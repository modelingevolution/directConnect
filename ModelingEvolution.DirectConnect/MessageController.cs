using System;
using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;

namespace ModelingEvolution.DirectConnect;

internal class SingleRequestController
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TypeRegister _typeRegister;
    private readonly ConcurrentDictionary<Type, Type> _adapterTypeByMessageType = new();
    public SingleRequestController(IServiceProvider serviceProvider, TypeRegister typeRegister)
    {
        _serviceProvider = serviceProvider;
        _typeRegister = typeRegister;
    }

    public async Task Dispatch(Guid messageId, ReadOnlyMemory<byte> data)
    {
        var messageType = _typeRegister.GetRequiredType(messageId);

        var message = Serializer.NonGeneric.Deserialize(messageType, data);

        var adapterType = _adapterTypeByMessageType.GetOrAdd(messageType, x => typeof(RequestHandlerAdapter<>).MakeGenericType(x));

        var adapter = (IRequestHandlerAdapter)_serviceProvider.GetRequiredService(adapterType);

        await adapter.Handle(message);
    }
}

readonly record struct InvocationResult(ReadOnlyMemory<byte> MessageId, ReadOnlyMemory<byte> Payload);
internal class RequestResponseController
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TypeRegister _typeRegister;
    private readonly ConcurrentDictionary<Type, Type> _adapterTypeByMessageType = new();
    private readonly ArrayBufferWriter<byte> _buffer = new();
    public RequestResponseController(IServiceProvider serviceProvider, TypeRegister typeRegister)
    {
        _serviceProvider = serviceProvider;
        _typeRegister = typeRegister;
    }

    public async Task<InvocationResult> Dispatch(Guid messageId, ReadOnlyMemory<byte> data)
    {
        var messageType = _typeRegister.GetRequiredType(messageId);

        var message = Serializer.NonGeneric.Deserialize(messageType, data);

        var adapterType = _adapterTypeByMessageType.GetOrAdd(messageType, x => typeof(IRequestResponseHandlerAdapter<>).MakeGenericType(x));

        var adapter = (IRequestResponseHandlerAdapter)_serviceProvider.GetRequiredService(adapterType);

        var result = await adapter.Handle(message);

        // Not every efficient, because of memory copying.
        var retType = result.GetType().NameHash();
        _buffer.Clear();
        Serializer.Serialize(_buffer, result);

        InvocationResult ret = new InvocationResult(retType.AsMemory(), _buffer.WrittenMemory);
        return ret;
    }
}