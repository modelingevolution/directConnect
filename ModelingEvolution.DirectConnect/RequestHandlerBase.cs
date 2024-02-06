namespace ModelingEvolution.DirectConnect;

public abstract class RequestHandlerBase<TRequest> : IRequestHandler<TRequest>
{
    public abstract Task Handle(TRequest request);

    public Task Handle(object request)
    {
        return Handle((TRequest)request);
    }
}