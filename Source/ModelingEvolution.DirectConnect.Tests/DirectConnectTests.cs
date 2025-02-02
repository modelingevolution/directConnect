using System.Buffers;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ProtoBuf;

namespace ModelingEvolution.DirectConnect.Tests;

public class BlobHandler : IBlobRequestHandler<FooVoidRequest>
{
    public async IAsyncEnumerable<IMemoryOwner<byte>> Handle(FooVoidRequest request, IBlobContext context)
    {
        await context.Metadata(new FooResponse() { Name = request.Name });
        
        yield return ReturnChunk(69);
        yield return ReturnChunk(66);
        yield return ReturnChunk(99);
    }

    private static IMemoryOwner<byte> ReturnChunk(byte tmp)
    {
        var data = MemoryPool<byte>.Shared.Rent(1);
        data.Memory.Span[0] = tmp;
        return data;
    }
}

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
        service.AddSingleton<RequestDispatcher>();
        service.AddSingleton<RequestResponseController>();
        service.AddRequest<FooVoidRequest>();
        var customHandler = Substitute.For<IRequestHandler<FooVoidRequest>>();
        service.AddSingleton<IRequestHandler<FooVoidRequest>>(customHandler);
        IServiceProvider sp = service.BuildServiceProvider();
            
          
        var handler =sp.GetRequiredService<RequestDispatcher>();
        var (data, messageId )= Example();
        await handler.Dispatch(messageId, new ReadOnlyMemory<byte>(data));

        await customHandler.Received(1).Handle(Arg.Is<FooVoidRequest>(x => x.Name == "Test"));
    }

    [Fact]
    public async Task BlobRequest()
    {
        using ServerApp srv = new ServerApp();
        

        await srv.StartAsync(x =>
        {
            x.AddBlobHandler<FooVoidRequest,BlobHandler>()
                .AddServerDirectConnect();
        });

        using var client = new ClientApp();

        var sp = client.Start(service => service.AddClientDirectConnect().AddMessage<FooResponse>());

        var blobFactory = sp.GetRequiredService<BlobClientFactory>();
        var invoker = blobFactory.CreateClient<FooVoidRequest>("http://localhost:5001");
        
        var stream = await invoker.Execute<FooResponse>(new FooVoidRequest() { Name="Foo69"});
        stream.Metadata.Name.Should().Be("Foo69");

        List<byte> tmp = new();
        await foreach (var i in stream.Chunks())
        {
            tmp.Add(i.Span[0]);
        }

        tmp.Should().HaveCount(3);
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
    [Fact]
    public async Task RequestArrayResponse()
    {
        using ServerApp srv = new ServerApp();
        var customHandler = Substitute.For<IRequestHandler<FooRequest, IFooResponse[]>>();
        customHandler.Handle(Arg.Any<FooRequest>()).Returns(Task.FromResult((IFooResponse[])(new []
        {
            new FooResponse() { Name = "Test2" },
            new FooResponse() { Name = "Test3" }
        })));

        await srv.StartAsync(x =>
        {
            x.AddRequest<FooVoidRequest>()
                .AddRequestResponse<FooRequest, IFooResponse[]>()
                .AddSingleton(customHandler)
                .AddMessage<FooResponse>()
                .AddServerDirectConnect();
        });

        using var client = new ClientApp();

        var sp = client.Start(service => service.AddClientDirectConnect()
            .AddClientInvoker<FooVoidRequest>()
            .AddMessage<FooResponse>()
            .AddClientInvoker<FooRequest, IFooResponse[]>());

        var clientPool = sp.GetRequiredService<IRequestInvokerPool>();
        var invoker = clientPool.Get("http://localhost:5001");
        var ret = await invoker.Invoke<FooRequest, IFooResponse[]>(new FooRequest());
        ret[0].Name.Should().Be("Test2");

        await customHandler.Received(1).Handle(Arg.Is<FooRequest>(x => x.Name == "Test"));
    }


}