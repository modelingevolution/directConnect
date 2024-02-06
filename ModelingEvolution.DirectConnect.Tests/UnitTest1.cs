using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NSubstitute;
using ProtoBuf;
using ProtoBuf.Meta;

namespace ModelingEvolution.DirectConnect.Tests
{
    [ProtoContract]
    public class FooRequest
    {
        [ProtoMember(1)] public string Name { get; set; } = "Test";
    }

   
    public class UnitTest1
    {
        

        private (byte[], Guid) Example()
        {
            var buffer = new ArrayBufferWriter<byte>();
            Serializer.Serialize(buffer, new FooRequest());
            return (buffer.WrittenSpan.ToArray(), typeof(FooRequest).NameId());
            
        }

        [Fact]
        public async Task RunningServerTest()
        {
            IServiceCollection service = new ServiceCollection();
            service.AddSingleton<MessageController>();
            service.AddRequest<FooRequest>();
            var customHandler = Substitute.For<IRequestHandler<FooRequest>>();
            service.AddSingleton<IRequestHandler<FooRequest>>(customHandler);
            IServiceProvider sp = service.BuildServiceProvider();
            
          
            var handler =sp.GetRequiredService<MessageController>();
            var (data, messageId )= Example();
            await handler.Dispatch(messageId,data);

            await customHandler.Received(1).Handle(Arg.Is<FooRequest>(x => x.Name == "Test"));
        }

        [Fact]
        public void RunningClientTest()
        {
            IServiceCollection service = new ServiceCollection();
            service.AddRequestSink();
            var sp = service.BuildServiceProvider();

            sp.GetRequiredService<IRequestHandler<Message>>().Handle(new Message());
            sp.GetRequiredService<IRequestInvoker>().Invoke(new Message());
        }

        [Fact]
        public void ConnectionBeetwenServerAndClientTest()
        {

        }
    }

    public interface IRequestInvoker
    {
        Task Invoke<TRequest>(TRequest request);
    }
}