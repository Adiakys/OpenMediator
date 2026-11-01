using FluentAssertions;
using Moq;
using AnyMediator.NotificationPublishers;
using AnyMediator.NotificationSinks;

namespace AnyMediator.Tests;

public sealed class InMemoryNotificationSinkTests
{
    [Fact]
    public async Task Dispatch_WithMultipleHandlers_ShouldExecuteAll()
    {
        var handler1 = new TestHandler();
        var handler2 = new TestHandler();

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(new List<INotificationHandler<TestNotification>> { handler1, handler2 });

        var sink = new InMemoryNotificationSink(services.Object, new HandlerRegistry(), new ForeachAwaitPublisher());

        await sink.Dispatch(new TestNotification());

        handler1.NotificationCount.Should().Be(1);
        handler2.NotificationCount.Should().Be(1);
    }

    [Fact]
    public async Task Dispatch_WhenHandlerThrows_ShouldPropagateOriginalException()
    {
        var expectedException = new InvalidOperationException("handler failed");
        var handler = new Mock<INotificationHandler<TestNotification>>();
        handler.Setup(x => x.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(new List<INotificationHandler<TestNotification>> { handler.Object });

        var sink = new InMemoryNotificationSink(services.Object, new HandlerRegistry(), new ForeachAwaitPublisher());

        var act = () => sink.Dispatch(new TestNotification());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task Dispatch_WithForeachAwaitPublisher_RunsHandlersInOrderAndStopsAtTheFirstFailure()
    {
        var calls = new List<string>();
        var first = new Mock<INotificationHandler<TestNotification>>();
        first.Setup(x => x.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(() => { calls.Add("first"); return Task.CompletedTask; });
        var failing = new Mock<INotificationHandler<TestNotification>>();
        failing.Setup(x => x.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { await Task.Yield(); calls.Add("failing"); throw new InvalidOperationException("handler failed"); });
        var last = new Mock<INotificationHandler<TestNotification>>();
        last.Setup(x => x.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(() => { calls.Add("last"); return Task.CompletedTask; });

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(new[] { first.Object, failing.Object, last.Object });

        var sink = new InMemoryNotificationSink(services.Object, new HandlerRegistry(), new ForeachAwaitPublisher());

        var act = () => sink.Dispatch(new TestNotification());

        await act.Should().ThrowAsync<InvalidOperationException>();
        calls.Should().Equal("first", "failing");
    }

    [Fact]
    public async Task Dispatch_WithNoHandlers_ShouldNotThrow()
    {
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(new List<INotificationHandler<TestNotification>>());

        var sink = new InMemoryNotificationSink(services.Object, new HandlerRegistry(), new ForeachAwaitPublisher());

        var act = () => sink.Dispatch(new TestNotification());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Dispatch_WithTaskWhenAllPublisher_ShouldExecuteAllHandlers()
    {
        var handler1 = new TestHandler();
        var handler2 = new TestHandler();

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(new List<INotificationHandler<TestNotification>> { handler1, handler2 });

        var sink = new InMemoryNotificationSink(services.Object, new HandlerRegistry(), new TaskWhenAllPublisher());

        await sink.Dispatch(new TestNotification());

        handler1.NotificationCount.Should().Be(1);
        handler2.NotificationCount.Should().Be(1);
    }

    [Fact]
    public async Task Dispatch_WithContinueOnExceptionPublisher_RunsAllHandlersAndAggregatesExceptions()
    {
        var ok = new TestHandler();
        var firstFailure = new InvalidOperationException("first");
        var secondFailure = new InvalidOperationException("second");

        var failing1 = new Mock<INotificationHandler<TestNotification>>();
        failing1.Setup(x => x.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(firstFailure);
        var failing2 = new Mock<INotificationHandler<TestNotification>>();
        failing2.Setup(x => x.Handle(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(secondFailure);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(new List<INotificationHandler<TestNotification>> { failing1.Object, ok, failing2.Object });

        var sink = new InMemoryNotificationSink(services.Object, new HandlerRegistry(), new ForeachAwaitContinueOnExceptionPublisher());

        var act = () => sink.Dispatch(new TestNotification());

        var thrown = (await act.Should().ThrowAsync<AggregateException>()).Which;
        thrown.InnerExceptions.Should().HaveCount(2);
        thrown.InnerExceptions.Should().BeEquivalentTo(new[] { firstFailure, secondFailure });
        ok.NotificationCount.Should().Be(1);
    }

    [Fact]
    public async Task Dispatch_WithContinueOnExceptionPublisher_WhenNoHandlerThrows_DoesNotThrow()
    {
        var handler1 = new TestHandler();
        var handler2 = new TestHandler();

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationHandler<TestNotification>>)))
            .Returns(new List<INotificationHandler<TestNotification>> { handler1, handler2 });

        var sink = new InMemoryNotificationSink(services.Object, new HandlerRegistry(), new ForeachAwaitContinueOnExceptionPublisher());

        await sink.Dispatch(new TestNotification());

        handler1.NotificationCount.Should().Be(1);
        handler2.NotificationCount.Should().Be(1);
    }
}
