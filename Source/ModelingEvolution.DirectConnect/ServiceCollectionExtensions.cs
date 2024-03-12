using System.Buffers;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelingEvolution.DirectConnect.Grpc;
using ProtoBuf;
using ProtoBuf.Meta;

namespace ModelingEvolution.DirectConnect;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerDirectConnect(this IServiceCollection services)
    {
        services.AddSingleton<SingleRequestController>();
        services.AddSingleton<RequestResponseController>();
        return services;
    }

    public static IServiceCollection AddClientDirectConnect(this IServiceCollection services)
    {
        services.AddScoped<IRequestInvoker, RequestInvoker>();
        services.AddScoped<GrpcChannelScopeFactory>();
        services.AddScoped<GrpcChannel>(sp => sp.GetRequiredService<GrpcChannelScopeFactory>().Factory());
        services.AddSingleton<IRequestInvokerPool, RequestInvokerPool>();
        return services;
    }

    public static IEndpointRouteBuilder MapDirectConnect(this IEndpointRouteBuilder builder)
    {
        builder.MapGrpcService<GrpcRequestController>();
        return builder;
    }

    public static IServiceCollection AddMessage<TMessage>(this IServiceCollection services)
    {
        var types = new [] {typeof(TMessage)};

        services.AddMessages(types);
        return services;
    }

    public static IServiceCollection AddMessages(this IServiceCollection services, IEnumerable<Type> types)
    {
        if (!services.TryGetSingleton<TypeRegister>(out var registry))
            services.AddSingleton(registry = new TypeRegister());

        registry.Index(types);
        return services;
    }

    public static IServiceCollection AddRequest<TRequest>(this IServiceCollection services)
    {
        if (!services.TryGetSingleton<TypeRegister>(out var registry))
            services.AddSingleton(registry = new TypeRegister());

        registry.Index(typeof(TRequest));
        services.AddSingleton<RequestHandlerAdapter<TRequest>>();
        return services;
    }
    public static IServiceCollection AddRequestResponse<TRequest,TResponse>(this IServiceCollection services)
    {
        return services.AddRequestResponse(typeof(TRequest), typeof(TResponse));
    }
    public static IServiceCollection AddRequestResponse(this IServiceCollection services, Type requestType, Type responseType)
    {
        if (!services.TryGetSingleton<TypeRegister>(out var registry))
            services.AddSingleton(registry = new TypeRegister());

        registry.Index(requestType);
        if (!responseType.IsArray)
            registry.Index(responseType);
        else
        {
            var elementType = responseType.GetElementType();
            if (!elementType.IsInterface)
                registry.Index(elementType);
        }

        var serviceType = typeof(IRequestResponseHandlerAdapter<>).MakeGenericType(requestType);
        var implementationType = typeof(RequestHandlerAdapter<,>).MakeGenericType(requestType, responseType);
        services.AddSingleton(serviceType, implementationType);

        return services;
    }
    public static IServiceCollection AddRequestHandler<TRequest, TRequestHandler>(this IServiceCollection services)
    where TRequestHandler: class, IRequestHandler<TRequest>
    {
        services.AddRequest<TRequest>();

        services.AddScoped<IRequestHandler<TRequest>, TRequestHandler>();
       
        return services;
    }
    public static IServiceCollection AddClientInvoker<TRequest>(this IServiceCollection services)
    {
        if (!services.TryGetSingleton<TypeRegister>(out var registry))
            services.AddSingleton(registry = new TypeRegister());

        registry.Index(typeof(TRequest));

        services.AddScoped<IRequestHandler<TRequest>, RequestInvoker<TRequest>>();

        return services;
    }
    public static IServiceCollection AddClientInvoker(this IServiceCollection services, Type requestType, Type responseType)
    {
        if (!services.TryGetSingleton<TypeRegister>(out var registry))
            services.AddSingleton(registry = new TypeRegister());

        registry.Index(requestType);

        if (!responseType.IsArray)
            registry.Index(responseType);
        else
        {
            var elementType = responseType.GetElementType();
            if (!elementType.IsInterface)
                registry.Index(elementType);
        }

        var srv = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var impl = typeof(RequestInvoker<,>).MakeGenericType(requestType, responseType);
        
        services.AddScoped(srv,impl);

        return services;
    }
    public static IServiceCollection AddClientInvoker<TRequest,TResponse>(this IServiceCollection services) => services.AddClientInvoker(typeof(TRequest), typeof(TResponse));
}

