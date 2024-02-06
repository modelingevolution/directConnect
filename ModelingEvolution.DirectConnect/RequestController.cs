using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;

namespace ModelingEvolution.DirectConnect;

public class MessageController
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TypeRegister _typeRegister;
    private readonly ConcurrentDictionary<Type, Type> _adapterTypeByMessageType = new();
    public MessageController(IServiceProvider serviceProvider, TypeRegister typeRegister)
    {
        _serviceProvider = serviceProvider;
        _typeRegister = typeRegister;
    }

    public async Task Dispatch(Guid messageId, byte[] data)
    {
        var messageType = _typeRegister.GetRequiredType(messageId);

        var message = Serializer.NonGeneric.Deserialize(messageType, data.AsSpan());

        var adapterType = _adapterTypeByMessageType.GetOrAdd(messageType, x => typeof(RequestHandlerAdapterAdapter<>).MakeGenericType(x));

        var adapter = (IRequestHandlerAdapter)_serviceProvider.GetRequiredService(adapterType);

        await adapter.Handle(message);
    }
}