using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ModelingEvolution.DirectConnect;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRequest<TRequest>(this IServiceCollection services)
    {
        if (!services.TryGetSingleton<TypeRegister>(out var registry))
            services.AddSingleton(registry = new TypeRegister());

        registry.Index(typeof(TRequest));
        services.AddSingleton<RequestHandlerAdapterAdapter<TRequest>>();
        return services;
    }
    public static IServiceCollection AddRequestHandler<TRequest, TRequestHandler>(this IServiceCollection services)
    where TRequestHandler: class, IRequestHandler<TRequest>
    {
        services.AddRequest<TRequest>();

        services.AddSingleton<IRequestHandler<TRequest>, TRequestHandler>();
       
        return services;
    }
    public static IServiceCollection AddRequestSink(this IServiceCollection services)
    {
        //services.AddSingleton(new TypeRegister().Index(typeof(Message)));
        //services.AddSingleton<IRequestHandler<Message>, RequestSink<Message>>();

        return services;
    }
}