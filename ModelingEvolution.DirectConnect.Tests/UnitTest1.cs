using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;
using ProtoBuf.Meta;

namespace ModelingEvolution.DirectConnect.Tests
{
    public class UnitTest1
    {
        

        private Message Example()
        {
            var buffer = new ArrayBufferWriter<byte>();
            Serializer.Serialize(buffer, new Message());
            Message m = new Message()
            {
                Data = buffer.WrittenSpan.ToArray(),
                TypeId = typeof(Message).NameId()
            };
            return m;
        }

        [Fact]
        public void RunningServerTest()
        {
            IServiceCollection service = new ServiceCollection();
            service.AddRequestHandler();
            IServiceProvider sp = service.BuildServiceProvider();
            
          
            var handler =sp.GetRequiredService<MessageRequestHandler>();
            var msg = Example();
            handler.Handle(msg);


            //Jakis IRequestHandler zostanie wywolany.
            //sp.GetRequiredService<FooRequest>().Received(1).M;
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