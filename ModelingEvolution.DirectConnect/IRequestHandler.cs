namespace ModelingEvolution.DirectConnect;

public interface IRequestHandler<in TRequest> 
{

    Task Handle(TRequest request);
}

public interface IRequestHandlerAdapter
{
    Task Handle(object request);
}