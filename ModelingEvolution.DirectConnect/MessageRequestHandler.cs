using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;

namespace ModelingEvolution.DirectConnect;

public class MessageRequestHandler : RequestHandlerBase<Message>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TypeRegister _typeRegister;

    public MessageRequestHandler(IServiceProvider service)
    {
        _serviceProvider = service;
        _typeRegister =  _serviceProvider.GetRequiredService<TypeRegister>();
    }
    public override async Task Handle(Message request)
    {
        var typeOfFooRequest = _typeRegister.GetRequiredType(request.TypeId);

        Type type = typeof(IRequestHandler<>).MakeGenericType(typeOfFooRequest);

        var handler = (IRequestHandler)_serviceProvider.GetRequiredService(type);
        var deserializedObj = Serializer.NonGeneric.Deserialize(typeOfFooRequest, request.Data.AsSpan());
        handler.Handle(deserializedObj);
    }
        
}