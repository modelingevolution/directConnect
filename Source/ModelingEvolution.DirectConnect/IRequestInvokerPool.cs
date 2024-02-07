namespace ModelingEvolution.DirectConnect;

public interface IRequestInvokerPool
{
    IRequestInvoker Get(string url);
}