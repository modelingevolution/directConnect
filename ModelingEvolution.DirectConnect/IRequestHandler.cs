namespace ModelingEvolution.DirectConnect;

public interface IRequestHandler<in TRequest> : IRequestHandler
{

    Task Handle(TRequest request);
}

public interface IRequestHandler
{
    Task Handle(object request);
}