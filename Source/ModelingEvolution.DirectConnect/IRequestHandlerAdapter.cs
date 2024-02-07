namespace ModelingEvolution.DirectConnect;

internal interface IRequestHandlerAdapter
{
    Task Handle(object request);
}
internal interface IRequestResponseHandlerAdapter
{
    Task<object> Handle(object request);
}
internal interface IRequestResponseHandlerAdapter<TRequest> : IRequestResponseHandlerAdapter
{
    
}