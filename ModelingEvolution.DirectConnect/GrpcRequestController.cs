using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ModelingEvolution.DirectConnect.Grpc;

namespace ModelingEvolution.DirectConnect;

class GrpcRequestController : Grpc.RequestController.RequestControllerBase
{
    private readonly SingleRequestController _singleRequestController;
    private readonly RequestResponseController _requestResponseController;
    private static readonly Empty empty = new Empty();
    public GrpcRequestController(SingleRequestController singleRequestController, RequestResponseController requestResponseController)
    {
        _singleRequestController = singleRequestController;
        _requestResponseController = requestResponseController;
    }

    

    public override async Task<Empty> SendVoid(DirectMessage request, ServerCallContext context)
    {
        await _singleRequestController.Dispatch(new Guid(request.MessageId.Span), request.Data.Memory);
        return empty;
    }

    public override async Task<DirectMessage> Send(DirectMessage request, ServerCallContext context)
    {
        var result = await _requestResponseController.Dispatch(new Guid(request.MessageId.Span), request.Data.Memory);

        return new DirectMessage()
        {
            MessageId = ByteString.CopyFrom(result.MessageId.Span),
            Data = ByteString.CopyFrom(result.Payload.Span)
        };
    }
}