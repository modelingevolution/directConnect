using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.DirectConnect.Grpc;
using ProtoBuf;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Grpc.Core;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ModelingEvolution.DirectConnect;

public record BlobStream<TMetadata>
{
    internal BlobStream(AsyncServerStreamingCall<DownloadReply> data, TypeRegister typeRegister)
    {
        _data = data;
        _typeRegister = typeRegister;
    }

    public TMetadata Metadata { get; private set; }
    private readonly AsyncServerStreamingCall<DownloadReply> _data;
    private readonly TypeRegister _typeRegister;
    private ReadOnlyMemory<byte>? _fistChunk;
    internal async Task Init()
    {
        if (await _data.ResponseStream.MoveNext(default))
        {
            switch (_data.ResponseStream.Current.PayloadCase)
            {
                case DownloadReply.PayloadOneofCase.None:
                    break;
                case DownloadReply.PayloadOneofCase.Metadata:
                    {
                        var tid = new Guid(_data.ResponseStream.Current.Metadata.MessageId.Span);
                        var mtype = _typeRegister.GetRequiredType(tid) ?? throw new ArgumentException("Type register is missing an entry.");
                        Metadata = (TMetadata)Serializer.NonGeneric.Deserialize(mtype, _data.ResponseStream.Current.Metadata.Data.Span);
                    }
                    break;
                case DownloadReply.PayloadOneofCase.Data:
                    _fistChunk = _data.ResponseStream.Current.Data.Memory;
                    break;
                case DownloadReply.PayloadOneofCase.Fault:
                    {
                        var tid = new Guid(_data.ResponseStream.Current.Fault.MessageId.Span);
                        var mtype = _typeRegister.GetRequiredType(tid) ?? throw new ArgumentException("Type register is missing an entry.");
                        var fault = Serializer.NonGeneric.Deserialize(mtype, _data.ResponseStream.Current.Metadata.Data.Span);
                        throw FaultException.Create(fault);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
    }
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks()
    {
        if (_fistChunk.HasValue)
            yield return _fistChunk.Value;
        
        while (await _data.ResponseStream.MoveNext(default))
        {
            switch (_data.ResponseStream.Current.PayloadCase)
            {
                case DownloadReply.PayloadOneofCase.None:
                case DownloadReply.PayloadOneofCase.Metadata:
                    break;
                case DownloadReply.PayloadOneofCase.Fault:
                    var tid = new Guid(_data.ResponseStream.Current.Fault.MessageId.Span);
                    var mtype = _typeRegister.GetRequiredType(tid) ?? throw new ArgumentException("Type register is missing an entry.");
                    var fault = Serializer.NonGeneric.Deserialize(mtype, _data.ResponseStream.Current.Metadata.Data.Span);
                    throw FaultException.Create(fault);
                    break;
                case DownloadReply.PayloadOneofCase.Data:
                    yield return _data.ResponseStream.Current.Data.Memory;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
    }

}

public interface IBlobInvoker<in TRequest>
{
    Task<BlobStream<TMetadata>> Execute<TMetadata>(TRequest request);
}
class BlobRequestInvoker<TRequest>(GrpcChannel channel, TypeRegister typeRegister) : IBlobInvoker<TRequest>
{
    
    public async Task<BlobStream<TMetadata>> Execute<TMetadata>(TRequest request)
    {
        ObjectSerializer s = new ObjectSerializer().Init();
        var d = s.Serialize(request);
        var proxy = new Grpc.BlobController.BlobControllerClient(channel);
        var env= new ObjectEvenlope()
        {
            MessageId = ByteString.CopyFrom(d.MessageId.Span),
            Data = ByteString.CopyFrom(d.Payload.Span)
        };
        var data = proxy.Download(env);
        var stream = new BlobStream<TMetadata>(data, typeRegister);
        await stream.Init();
        return stream;
    }
}

public class ChannelPool : IDisposable
{
    private readonly ConcurrentDictionary<Uri, GrpcChannel> _channel = new();

    public GrpcChannel GetChannel(string address) => GetChannel(new Uri(address));
    
    public GrpcChannel GetChannel(Uri address)
    {
        return _channel.GetOrAdd(address, x => GrpcChannel.ForAddress(address));
    }
    public void Dispose()
    {
        foreach (var i in _channel.Values)
            i.Dispose();
        _channel.Clear();
    }
}
public class BlobClientFactory
{
    
    private readonly TypeRegister _typeRegister;
    private readonly ChannelPool _pool;

    public BlobClientFactory(TypeRegister typeRegister, ChannelPool pool)
    {
        _typeRegister = typeRegister;
        _pool = pool;
    }

    public IBlobInvoker<TRequest> CreateClient<TRequest>(string address)
    {
        var channel = _pool.GetChannel(address);
        return CreateClient<TRequest>(channel);
    }
    public IBlobInvoker<TRequest> CreateClient<TRequest>(GrpcChannel channel)
    {
        return new BlobRequestInvoker<TRequest>(channel, _typeRegister);
    }

    
}

class RequestInvoker<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
{
    private readonly GrpcChannel _channel;
    private readonly TypeRegister _typeRegister;
    private readonly byte[] _messageId;
    private readonly ArrayBufferWriter<byte> _buffer;
    public RequestInvoker(GrpcChannel channel, TypeRegister typeRegister)
    {
        _channel = channel;
        _typeRegister = typeRegister;
        _messageId = typeof(TRequest).NameHash();
        _buffer = new ArrayBufferWriter<byte>();
    }

    public async Task<TResponse> Handle(TRequest request)
    {
        var proxy = new Grpc.RequestController.RequestControllerClient(_channel);
        _buffer.Clear();
        Serializer.Serialize(_buffer, request);

        var result = await proxy.SendAsync(new ObjectEvenlope()
        {
            Data = ByteString.CopyFrom(_buffer.WrittenSpan),
            MessageId = ByteString.CopyFrom(_messageId)
        });
        switch (result.PayloadCase)
        {
            case Reply.PayloadOneofCase.Object:
                var type = _typeRegister.GetRequiredType(new Guid(result.Object.MessageId.Span));
                return Serializer.NonGeneric.Deserialize(type, result.Object.Data.Span) is TResponse r ? r : default(TResponse);

            case Reply.PayloadOneofCase.Array:
                var responseType = typeof(TResponse);
                if (responseType.IsArray)
                {
                    var items = result.Array.Items;
                    var array = Array.CreateInstance(responseType.GetElementType(), items.Count);

                    for (int i = 0; i < items.Count; i++)
                    {
                        var elemType = _typeRegister.GetRequiredType(new Guid(items[i].MessageId.Span));
                        var item = Serializer.NonGeneric.Deserialize(elemType, items[i].Data.Span);
                        array.SetValue(item, i);
                    }

                    return (TResponse)((object)array);
                }
                else throw new NotSupportedException($"Response {responseType.Name} is not an array.");

            case Reply.PayloadOneofCase.Fault:
                var faultType = _typeRegister.GetRequiredType(new Guid(result.Fault.MessageId.Span));
                var faultMsg = Serializer.NonGeneric.Deserialize(faultType, result.Fault.Data.Span);
                var exceptionType = typeof(FaultException<>).MakeGenericType(faultType);
                var exceptionObject = (Exception)Activator.CreateInstance(exceptionType, new object[] { faultMsg });
                throw exceptionObject;
                
           
            case Reply.PayloadOneofCase.None:
            case Reply.PayloadOneofCase.Empty:
            default:
                throw new ArgumentOutOfRangeException();
        }

       
    }
}
class RequestInvoker<TRequest> : IRequestHandler<TRequest>
{
    private readonly GrpcChannel _channel;
    private readonly byte[] _messageId;
    private readonly ArrayBufferWriter<byte> _buffer;
    private readonly TypeRegister _typeRegister;
    public RequestInvoker(GrpcChannel channel, TypeRegister typeRegister)
    {
        _channel = channel;
        _typeRegister = typeRegister;
        _messageId = typeof(TRequest).NameHash();
        _buffer = new ArrayBufferWriter<byte>();
    }

    public async Task Handle(TRequest request)
    {
        var proxy = new Grpc.RequestController.RequestControllerClient(_channel);
        _buffer.Clear();
        Serializer.Serialize(_buffer, request);

        var result = await proxy.SendVoidAsync(new ObjectEvenlope()
        {
            Data = ByteString.CopyFrom(_buffer.WrittenSpan),
            MessageId = ByteString.CopyFrom(_messageId)
        });
        switch (result.PayloadCase)
        {
            case Reply.PayloadOneofCase.Empty:
                return;
            case Reply.PayloadOneofCase.Fault:
                var faultType = _typeRegister.GetRequiredType(new Guid(result.Fault.MessageId.Span));
                var faultMsg = Serializer.NonGeneric.Deserialize(faultType, result.Fault.Data.Span);
                var exceptionType = typeof(FaultException<>).MakeGenericType(faultType);
                var exceptionObject = (Exception)Activator.CreateInstance(exceptionType, new object[] { faultMsg });
                throw exceptionObject;


            case Reply.PayloadOneofCase.None:
            case Reply.PayloadOneofCase.Object:
            default:
                throw new ArgumentOutOfRangeException();
        }
            ;
    }
}

class RequestInvoker : IRequestInvoker
{
    private readonly IServiceProvider _serviceProvider;

    public RequestInvoker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeVoid<TRequest>(TRequest request)
    {
        await _serviceProvider.GetRequiredService<IRequestHandler<TRequest>>().Handle(request);
    }

    public Task<TResponse> Invoke<TRequest, TResponse>(TRequest request)
    {
        return _serviceProvider.GetRequiredService<IRequestHandler<TRequest,TResponse>>().Handle(request);
    }
}