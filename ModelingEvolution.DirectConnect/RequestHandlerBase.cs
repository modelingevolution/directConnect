namespace ModelingEvolution.DirectConnect;

public abstract class RequestHandlerBase<TRequest> : IRequestHandler<TRequest>
{
    public abstract Task Handle(TRequest request);

    public Task Handle(object request)
    {
        return Handle((TRequest)request);
    }
}

public class RequestHandlerAdapterAdapter<TRequest> : IRequestHandlerAdapter
{
    private readonly IRequestHandler<TRequest> _handler;

    public RequestHandlerAdapterAdapter(IRequestHandler<TRequest> handler)
    {
        _handler = handler;
    }

    public async Task Handle(object request)
    {
        await _handler.Handle((TRequest)request);
    }
}