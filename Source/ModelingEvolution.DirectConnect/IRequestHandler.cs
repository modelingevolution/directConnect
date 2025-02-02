using System.Buffers;

namespace ModelingEvolution.DirectConnect;

public interface IRequestHandler<in TRequest> 
{
    Task Handle(TRequest request);
}

public interface IBlobRequestHandler
{
    IAsyncEnumerable<IMemoryOwner<byte>> Handle(object request, IBlobContext context);
}
public interface IBlobRequestHandler<in TRequest> : IBlobRequestHandler
{
    IAsyncEnumerable<IMemoryOwner<byte>> IBlobRequestHandler.Handle(object request, IBlobContext context) =>
        Handle((TRequest)request, context);
    IAsyncEnumerable<IMemoryOwner<byte>> Handle(TRequest request, IBlobContext context);
}

public interface IBlobContext
{
    Task Failed<T>(T obj);
    Task Metadata<T>(T obj);
}
