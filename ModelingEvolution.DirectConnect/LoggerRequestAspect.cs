using Microsoft.Extensions.Logging;

namespace ModelingEvolution.DirectConnect;

public class LoggerRequestAspect<TRequest> : IRequestHandler<TRequest>
{
    private readonly IRequestHandler<TRequest> _next;
    private readonly ILogger<TRequest> _logger;

    public LoggerRequestAspect(IRequestHandler<TRequest> next, ILogger<TRequest> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Handle(TRequest request)
    {
        try
        {
            await _next.Handle(request);
            _logger.LogInformation("Request {requestType} handled", typeof(TRequest).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Request {requestType} failed.", typeof(TRequest).Name);
        }
    }
}