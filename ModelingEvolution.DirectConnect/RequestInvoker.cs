using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.DirectConnect.Grpc;
using ProtoBuf;
using System.Buffers;
using System.Reflection;

namespace ModelingEvolution.DirectConnect;

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
            case Reply.PayloadOneofCase.Result:
                var type = _typeRegister.GetRequiredType(new Guid(result.Result.MessageId.Span));
                return Serializer.NonGeneric.Deserialize(type, result.Result.Data.Span) is TResponse r ? r : default(TResponse);
                
            case Reply.PayloadOneofCase.Fault:
                var faultType = _typeRegister.GetRequiredType(new Guid(result.Fault.MessageId.Span));
                var faultMsg = Serializer.NonGeneric.Deserialize(faultType, result.Fault.Data.Span);
                var exceptionType = typeof(FaultException<>).MakeGenericType(faultType);
                var exceptionObject = (Exception)Activator.CreateInstance(exceptionType, new object[] { faultMsg });
                throw exceptionObject;
                
            case Reply.PayloadOneofCase.Exception:
                throw result.Exception.ToArray().DeserializeAsException();

                break;
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

            case Reply.PayloadOneofCase.Exception:
                throw result.Exception.ToArray().DeserializeAsException();

            case Reply.PayloadOneofCase.None:
            case Reply.PayloadOneofCase.Result:
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