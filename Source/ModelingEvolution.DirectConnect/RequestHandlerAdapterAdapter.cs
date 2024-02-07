namespace ModelingEvolution.DirectConnect;

internal class RequestHandlerAdapter<TRequest> : IRequestHandlerAdapter
{
    private readonly IRequestHandler<TRequest> _handler;

    public RequestHandlerAdapter(IRequestHandler<TRequest> handler)
    {
        _handler = handler;
    }

    public async Task Handle(object request)
    {
        await _handler.Handle((TRequest)request);
    }
}
internal class RequestHandlerAdapter<TRequest,TResponse> : IRequestResponseHandlerAdapter<TRequest>
{
    private readonly IRequestHandler<TRequest,TResponse> _handler;

    public RequestHandlerAdapter(IRequestHandler<TRequest, TResponse> handler)
    {
        _handler = handler;
    }

    public async Task<object> Handle(object request)
    {
        return await _handler.Handle((TRequest)request);
    }
}