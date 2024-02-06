using Microsoft.Extensions.DependencyInjection;

namespace ModelingEvolution.DirectConnect;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSingletons(this IServiceCollection services, IEnumerable<Type> types)
    {
        foreach (var s in types) services.AddSingleton(s);
        return services;
    }
    public static IServiceCollection AddScopedServices(this IServiceCollection services, IEnumerable<Type> types)
    {
        foreach (var s in types) services.AddScoped(s);
        return services;
    }
    public static IServiceCollection AddScopedServices(this IServiceCollection services, Type openGeneric, IEnumerable<Type> types)
    {
        foreach (var concreteType in types.Where(x => x.IsClass && !x.IsAbstract))
        {
            var implementedInterfaces = concreteType.GetInterfaces()
                .Where(j => j.IsGenericType && !j.IsGenericTypeDefinition &&
                            j.GetGenericTypeDefinition() == openGeneric).ToArray();
            if (implementedInterfaces.Any())
                foreach (var service in implementedInterfaces)
                    services.AddScoped(service, concreteType);
        }
        return services;
    }
}