using System.Collections.Concurrent;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ModelingEvolution.DirectConnect;

/// <summary>
/// Could rise events - connected/disconnected.
/// </summary>
class RequestInvokerPool : IRequestInvokerPool, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, IServiceScope> _services = new();

    public RequestInvokerPool(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IRequestInvoker Get(string url)
    {
        return _services.GetOrAdd(url, x =>
        {
            var scope = _serviceProvider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<GrpcChannelScopeFactory>().Factory =
                () => GrpcChannel.ForAddress(url);
            return scope;
        }).ServiceProvider.GetRequiredService<IRequestInvoker>();
    }

    public void Dispose()
    {
        foreach(var i in _services.Values)
            i.Dispose();
    }
}