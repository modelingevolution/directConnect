using System.Buffers;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProtoBuf;

namespace ModelingEvolution.DirectConnect.Tests;

public class DirectConnectTests
{
    
    private (byte[], Guid) Example()
    {
        var buffer = new ArrayBufferWriter<byte>();
        Serializer.Serialize(buffer, new FooVoidRequest());
        return (buffer.WrittenSpan.ToArray(), typeof(FooVoidRequest).NameId());
            
    }

    [Fact]
    public async Task ServerDispatching()
    {
        IServiceCollection service = new ServiceCollection();
        service.AddSingleton<SingleRequestController>();
        service.AddSingleton<RequestResponseController>();
        service.AddRequest<FooVoidRequest>();
        var customHandler = Substitute.For<IRequestHandler<FooVoidRequest>>();
        service.AddSingleton<IRequestHandler<FooVoidRequest>>(customHandler);
        IServiceProvider sp = service.BuildServiceProvider();
            
          
        var handler =sp.GetRequiredService<SingleRequestController>();
        var (data, messageId )= Example();
        await handler.Dispatch(messageId, new ReadOnlyMemory<byte>(data));

        await customHandler.Received(1).Handle(Arg.Is<FooVoidRequest>(x => x.Name == "Test"));
    }

    

    [Fact]
    public async Task VoidRequest()
    {
        using ServerApp srv = new ServerApp();
        var customHandler = Substitute.For<IRequestHandler<FooVoidRequest>>();

        await srv.StartAsync(x =>
        {
            x.AddRequest<FooVoidRequest>()
                .AddSingleton(customHandler)
                .AddServerDirectConnect();
        });

        using var client = new ClientApp();
        
        var sp = client.Start(service => service.AddClientDirectConnect()
            .AddClientInvoker<FooVoidRequest>()
            .AddClientInvoker<FooRequest, FooResponse>());

        var clientPool = sp.GetRequiredService<IRequestInvokerPool>();
        var invoker = clientPool.Get("http://localhost:5001");
        await invoker.InvokeVoid(new FooVoidRequest());


        await customHandler.Received(1).Handle(Arg.Is<FooVoidRequest>(x => x.Name == "Test"));
    }
    [Fact]
    public async Task VoidRequestThrows()
    {
        using ServerApp srv = new ServerApp();
        var customHandler = Substitute.For<IRequestHandler<FooVoidRequest>>();
        customHandler.Handle(Arg.Any<FooVoidRequest>())
            .Throws(new FaultException<FooFailed>(new FooFailed()));

        await srv.StartAsync(x =>
        {
            x.AddRequest<FooVoidRequest>()
                .AddMessage<FooFailed>()
                .AddSingleton(customHandler)
                .AddServerDirectConnect();
        });

        using var client = new ClientApp();

        var sp = client.Start(service => service.AddClientDirectConnect()
            .AddMessage<FooFailed>()
            .AddClientInvoker<FooVoidRequest>()
            .AddClientInvoker<FooRequest, FooResponse>());

        var clientPool = sp.GetRequiredService<IRequestInvokerPool>();
        var invoker = clientPool.Get("http://localhost:5001");
        
        await Assert.ThrowsAsync<FaultException<FooFailed>>(async () => await invoker.InvokeVoid(new FooVoidRequest()));
        
    }
    [Fact]
    public async Task SystemExceptionsAreNotSupported()
    {
        using ServerApp srv = new ServerApp();
        var customHandler = Substitute.For<IRequestHandler<FooVoidRequest>>();
        customHandler.Handle(Arg.Any<FooVoidRequest>())
            .Throws<Exception>();

        await srv.StartAsync(x =>
        {
            x.AddRequest<FooVoidRequest>()
                .AddMessage<FooFailed>()
                .AddSingleton(customHandler)
                .AddServerDirectConnect();
        });

        using var client = new ClientApp();

        var sp = client.Start(service => service.AddClientDirectConnect()
            .AddMessage<FooFailed>()
            .AddClientInvoker<FooVoidRequest>()
            .AddClientInvoker<FooRequest, FooResponse>());

        var clientPool = sp.GetRequiredService<IRequestInvokerPool>();
        var invoker = clientPool.Get("http://localhost:5001");

        await Assert.ThrowsAsync<RpcException>(async () => await invoker.InvokeVoid(new FooVoidRequest()));

    }
    [Fact]
    public async Task RequestResponse()
    {
        using ServerApp srv = new ServerApp();
        var customHandler = Substitute.For<IRequestHandler<FooRequest, FooResponse>>();
        customHandler.Handle(Arg.Any<FooRequest>()).Returns(Task.FromResult(new FooResponse() { Name="Test2" }));

        await srv.StartAsync(x =>
        {
            x.AddRequest<FooVoidRequest>()
                .AddRequestResponse<FooRequest, FooResponse>()
                .AddSingleton(customHandler)
                .AddServerDirectConnect();
        });

        using var client = new ClientApp();

        var sp = client.Start(service => service.AddClientDirectConnect()
            .AddClientInvoker<FooVoidRequest>()
            .AddClientInvoker<FooRequest, FooResponse>());

        var clientPool = sp.GetRequiredService<IRequestInvokerPool>();
        var invoker = clientPool.Get("http://localhost:5001");
        var ret = await invoker.Invoke<FooRequest, FooResponse>(new FooRequest());
        ret.Name.Should().Be("Test2");

        await customHandler.Received(1).Handle(Arg.Is<FooRequest>(x => x.Name == "Test"));
    }


}