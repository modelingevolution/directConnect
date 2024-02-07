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

    

    public override async Task<Reply> SendVoid(ObjectEvenlope request, ServerCallContext context)
    {
        var ret = await _singleRequestController.Dispatch(new Guid(request.MessageId.Span), request.Data.Memory);
        return ret.ToReply();

    }

    public override async Task<Reply> Send(ObjectEvenlope request, ServerCallContext context)
    {
        var ret = await _requestResponseController.Dispatch(new Guid(request.MessageId.Span), request.Data.Memory);
        return ret.ToReply();
    }
}

static class RetFactory
{
    public static Reply ToReply(this InvocationResult ret)
    {
        switch (ret.Type)
        {
            case InvocationResultType.Void:
                return new Reply() { Empty = new Empty() };

            case InvocationResultType.Object:
                var objectResult = ret.Result.Value;
                return new Reply() { Result = new ObjectEvenlope() { Data = ByteString.CopyFrom(objectResult.Payload.Span), MessageId = ByteString.CopyFrom(objectResult.MessageId.Span) } };

            case InvocationResultType.Fault:
                var faultResult = ret.Result.Value;
                return new Reply() { Fault = new ObjectEvenlope() { Data = ByteString.CopyFrom(faultResult.Payload.Span), MessageId = ByteString.CopyFrom(faultResult.MessageId.Span) } };

            
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}