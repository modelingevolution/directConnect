namespace ModelingEvolution.DirectConnect;

public interface IRequestResponseHandler<in TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request);
}