using Microsoft.Extensions.DependencyInjection;

namespace ModelingEvolution.DirectConnect;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRequestHandler(this IServiceCollection services)
    {
        services.AddSingleton(new TypeRegister().Index(typeof(Message)));
        services.AddSingleton<IRequestHandler<Message>, MessageRequestHandler>();

        return services;
    }
    public static IServiceCollection AddRequestSink(this IServiceCollection services)
    {
        services.AddSingleton(new TypeRegister().Index(typeof(Message)));
        services.AddSingleton<IRequestHandler<Message>, RequestSink<Message>>();

        return services;
    }
}