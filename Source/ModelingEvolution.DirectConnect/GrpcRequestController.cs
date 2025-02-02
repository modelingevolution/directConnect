using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.DirectConnect.Grpc;
using ProtoBuf;
using ProtoBuf.Serializers;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ModelingEvolution.DirectConnect;

class BlobController(TypeRegister typeRegister, IServiceProvider serviceProvider) : Grpc.BlobController.BlobControllerBase
{
    class BlobContext(IServerStreamWriter<DownloadReply> responseStream) : IBlobContext
    {
        private readonly ObjectSerializer serializer = new ObjectSerializer().Init();
        public async Task Failed<T>(T obj)
        {
            var msg = serializer.Serialize(obj);
            await responseStream.WriteAsync(new DownloadReply
            {
                Fault = new ObjectEvenlope()
                {
                    MessageId = ByteString.CopyFrom(msg.MessageId.Span),
                    Data = ByteString.CopyFrom(msg.Payload.Span)
                }
            });
        }

        public async Task Metadata<T>(T obj)
        {
            var msg = serializer.Serialize(obj);
            await responseStream.WriteAsync(new DownloadReply
            {
                Metadata = new ObjectEvenlope()
                {
                    MessageId = ByteString.CopyFrom(msg.MessageId.Span),
                    Data = ByteString.CopyFrom(msg.Payload.Span)
                }
            });
        }
    }
    public override async Task Download(ObjectEvenlope request, IServerStreamWriter<DownloadReply> responseStream, ServerCallContext context)
    {
        var messageId = new Guid(request.MessageId.Span);
        var messageType = typeRegister.GetRequiredType(messageId);
        var message = Serializer.NonGeneric.Deserialize(messageType, request.Data.Memory);

        var handlerType = typeof(IBlobRequestHandler<>).MakeGenericType(messageType);
        var srv = (IBlobRequestHandler)serviceProvider.GetRequiredService(handlerType);

        
        BlobContext cx = new BlobContext(responseStream);

        await foreach (var m in srv.Handle(message, cx))
        {
            await responseStream.WriteAsync(new DownloadReply
            {
                Data = ByteString.CopyFrom(m.Memory.Span)
            });
            m.Dispose();
        }
    }

    
}


class GrpcRequestController(RequestDispatcher requestDispatcher, RequestResponseController requestResponseController)
    : Grpc.RequestController.RequestControllerBase
{
    private static readonly Empty empty = new Empty();


    public override async Task<Reply> SendVoid(ObjectEvenlope request, ServerCallContext context)
    {
        var ret = await requestDispatcher.Dispatch(new Guid(request.MessageId.Span), request.Data.Memory);
        return ret.ToReply();

    }

    public override async Task<Reply> Send(ObjectEvenlope request, ServerCallContext context)
    {
        var ret = await requestResponseController.Dispatch(new Guid(request.MessageId.Span), request.Data.Memory);
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
                return new Reply() { Object = new ObjectEvenlope() { Data = ByteString.CopyFrom(objectResult.Payload.Span), MessageId = ByteString.CopyFrom(objectResult.MessageId.Span) } };
            
            case InvocationResultType.Objects:
                var tmp = new Reply();
                tmp.Array = new ArrayEnvelope();
                tmp.Array.Items.Add(ret.Results.Select(x => new ObjectEvenlope()
                    { Data = ByteString.CopyFrom(x.Payload.Span), MessageId = ByteString.CopyFrom(x.MessageId.Span) }));
                return tmp;
            case InvocationResultType.Fault:
                var faultResult = ret.Result.Value;
                return new Reply() { Fault = new ObjectEvenlope() { Data = ByteString.CopyFrom(faultResult.Payload.Span), MessageId = ByteString.CopyFrom(faultResult.MessageId.Span) } };

            
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}