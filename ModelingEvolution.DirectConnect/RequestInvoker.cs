using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.DirectConnect.Grpc;
using ProtoBuf;
using System.Buffers;

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

        var result = await proxy.SendAsync(new DirectMessage()
        {
            Data = ByteString.CopyFrom(_buffer.WrittenSpan),
            MessageId = ByteString.CopyFrom(_messageId)
        });
        var type = _typeRegister.GetRequiredType(new Guid(result.MessageId.Span));
        return Serializer.NonGeneric.Deserialize(type,result.Data.Span) is TResponse r ? r : default(TResponse);
    }
}
class RequestInvoker<TRequest> : IRequestHandler<TRequest>
{
    private readonly GrpcChannel _channel;
    private readonly byte[] _messageId;
    private readonly ArrayBufferWriter<byte> _buffer;
    public RequestInvoker(GrpcChannel channel)
    {
        _channel = channel;
        _messageId = typeof(TRequest).NameHash();
        _buffer = new ArrayBufferWriter<byte>();
    }

    public async Task Handle(TRequest request)
    {
        var proxy = new Grpc.RequestController.RequestControllerClient(_channel);
        _buffer.Clear();
        Serializer.Serialize(_buffer, request);

        await proxy.SendVoidAsync(new DirectMessage()
        {
            Data = ByteString.CopyFrom(_buffer.WrittenSpan),
            MessageId = ByteString.CopyFrom(_messageId)
        });
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