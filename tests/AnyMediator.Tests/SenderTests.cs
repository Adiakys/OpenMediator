using FluentAssertions;
using Moq;

namespace AnyMediator.Tests;

public sealed class SenderTests
{
    [Fact]
    public async Task Send_WithNopipeline_ShouldReturnHandlerResult()
    {
        var handler = new TestHandler();
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        var sender = new AnyMediatorSender(services.Object, new HandlerRegistry());

        var result = await sender.Send(new TestRequest());

        result.Should().Be("Test");
    }

    [Fact]
    public async Task Send_WithPipelineBehaviors_ShouldExecuteAllBehaviorsAndHandler()
    {
        var handler = new TestHandler();
        var pipeline1 = new TestPipelineBehavior1<TestRequest, string>();
        var pipeline2 = new TestPipelineBehavior2<TestRequest, string>();
        var pipeline3 = new TestPipelineBehavior1<TestRequest, string>();

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        var pipelineServiceType = typeof(IEnumerable<>).MakeGenericType(typeof(IPipelineBehavior<TestRequest, string>));
        services.Setup(x => x.GetService(pipelineServiceType))
            .Returns(new List<IPipelineBehavior<TestRequest, string>> { pipeline1, pipeline2, pipeline3 });

        var sender = new AnyMediatorSender(services.Object, new HandlerRegistry());

        var result = await sender.Send(new TestRequest());

        result.Should().Be("Test");
        pipeline1.Count.Should().Be(1);
        pipeline2.Count.Should().Be(1);
        pipeline3.Count.Should().Be(1);
    }

    [Fact]
    public async Task Send_WhenHandlerThrows_ShouldPropagateOriginalException()
    {
        var expectedException = new InvalidOperationException("handler failed");
        var handler = new Mock<IRequestHandler<TestRequest, string>>();
        handler.Setup(x => x.Handle(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler.Object);

        var sender = new AnyMediatorSender(services.Object, new HandlerRegistry());

        var act = () => sender.Send(new TestRequest());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task Send_WhenPipelineThrows_ShouldPropagateOriginalException()
    {
        var expectedException = new InvalidOperationException("pipeline failed");
        var handler = new TestHandler();

        var pipeline = new Mock<IPipelineBehavior<TestRequest, string>>();
        pipeline.Setup(x => x.Handle(It.IsAny<TestRequest>(), It.IsAny<RequestHandlerDelegate<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler);

        var pipelineServiceType = typeof(IEnumerable<>).MakeGenericType(typeof(IPipelineBehavior<TestRequest, string>));
        services.Setup(x => x.GetService(pipelineServiceType))
            .Returns(new List<IPipelineBehavior<TestRequest, string>> { pipeline.Object });

        var sender = new AnyMediatorSender(services.Object, new HandlerRegistry());

        var act = () => sender.Send(new TestRequest());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task Send_ShouldPassCancellationTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken receivedToken = default;

        var handler = new Mock<IRequestHandler<TestRequest, string>>();
        handler.Setup(x => x.Handle(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TestRequest, CancellationToken>((_, ct) => receivedToken = ct)
            .ReturnsAsync("ok");

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler.Object);

        var sender = new AnyMediatorSender(services.Object, new HandlerRegistry());

        await sender.Send(new TestRequest(), cts.Token);

        receivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Send_WithPipeline_ShouldPassCancellationTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken receivedToken = default;

        var handler = new Mock<IRequestHandler<TestRequest, string>>();
        handler.Setup(x => x.Handle(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TestRequest, CancellationToken>((_, ct) => receivedToken = ct)
            .ReturnsAsync("ok");

        var pipeline = new TestPipelineBehavior1<TestRequest, string>();

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<TestRequest, string>)))
            .Returns(handler.Object);

        var pipelineServiceType = typeof(IEnumerable<>).MakeGenericType(typeof(IPipelineBehavior<TestRequest, string>));
        services.Setup(x => x.GetService(pipelineServiceType))
            .Returns(new List<IPipelineBehavior<TestRequest, string>> { pipeline });

        var sender = new AnyMediatorSender(services.Object, new HandlerRegistry());

        await sender.Send(new TestRequest(), cts.Token);

        receivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Send_VoidRequest_ShouldExecuteHandler()
    {
        var handler = new TestVoidHandler();
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<TestVoidRequest, Unit>)))
            .Returns(handler);

        var sender = new AnyMediatorSender(services.Object, new HandlerRegistry());

        await sender.Send(new TestVoidRequest());

        handler.Count.Should().Be(1);
    }

    [Fact]
    public async Task Send_VoidRequest_WithPipeline_ShouldExecuteBehaviorAndHandler()
    {
        var handler = new TestVoidHandler();
        var pipeline = new TestPipelineBehavior1<TestVoidRequest, Unit>();

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IRequestHandler<TestVoidRequest, Unit>)))
            .Returns(handler);

        var pipelineServiceType = typeof(IEnumerable<>).MakeGenericType(typeof(IPipelineBehavior<TestVoidRequest, Unit>));
        services.Setup(x => x.GetService(pipelineServiceType))
            .Returns(new List<IPipelineBehavior<TestVoidRequest, Unit>> { pipeline });

        var sender = new AnyMediatorSender(services.Object, new HandlerRegistry());

        await sender.Send(new TestVoidRequest());

        handler.Count.Should().Be(1);
        pipeline.Count.Should().Be(1);
    }
}
