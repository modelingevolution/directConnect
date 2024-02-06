using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection.Metadata;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;
using ProtoBuf.Meta;

namespace ModelingEvolution.DirectConnect.Tests
{
    [ProtoContract]
    public class Message
    {
        
        [ProtoMember(1)]
        public Guid TypeId { get; set; }
        [ProtoMember(2)]
        public byte[] Data { get; set; }
    }

    public interface IRequestHandler
    {
        Task Handle(object request);
    }
    public interface IRequestResponseHandler<in TRequest, TResponse>
    {
        Task<TResponse> Handle(TRequest request);
    }
    public interface IRequestHandler<in TRequest> : IRequestHandler
    {

        Task Handle(TRequest request);
    }

    public abstract class RequestHandlerBase<TRequest> : IRequestHandler<TRequest>
    {
        public abstract Task Handle(TRequest request);

        public Task Handle(object request)
        {
            return Handle((TRequest)request);
        }
    }
    public class MessageRequestHandler : RequestHandlerBase<Message>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TypeRegister _typeRegister;

        public MessageRequestHandler(IServiceProvider service)
        {
          _serviceProvider = service;
          _typeRegister =  _serviceProvider.GetRequiredService<TypeRegister>();
        }
        public override async Task Handle(Message request)
        {
            var typeOfFooRequest = _typeRegister.GetRequiredType(request.TypeId);

            Type type = typeof(IRequestHandler<>).MakeGenericType(typeOfFooRequest);

            var handler = (IRequestHandler)_serviceProvider.GetRequiredService(type);
            var deserializedObj = Serializer.NonGeneric.Deserialize(typeOfFooRequest, request.Data.AsSpan());
            handler.Handle(deserializedObj);
        }
        
    }

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

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRequestHandler(this IServiceCollection services)
        {
            services.AddSingleton(new TypeRegister().Index(typeof(Message)));
            services.AddSingleton<IRequestHandler<Message>, MessageRequestHandler>();

            return services;
        }
        public static IServiceCollection AddRequestSink(this IServiceCollection services)
        {
            services.AddSingleton(new TypeRegister().Index(typeof(Message)));
            services.AddSingleton<IRequestHandler<Message>, RequestSink<Message>>();

            return services;
        }
    }


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