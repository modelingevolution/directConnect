namespace ModelingEvolution.DirectConnect;

public class RequestSink<TRequest>: IRequestHandler<Message>
{
    public Task Handle(Message request)
    {
        // Tutal lecimy  w kanal GRPC.
    }

    public Task Handle(object request)
    {
        return Handle((TRequest)request);
    }
}