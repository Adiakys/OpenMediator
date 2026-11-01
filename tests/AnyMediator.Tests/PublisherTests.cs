using FluentAssertions;
using Moq;

namespace AnyMediator.Tests;

public sealed class PublisherTests
{
    [Fact]
    public async Task Publish_WithMultipleSinks_ShouldDispatchToAll()
    {
        var sink1 = new Mock<INotificationSink>();
        sink1.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sink2 = new Mock<INotificationSink>();
        sink2.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationSink>)))
            .Returns(new[] { sink1.Object, sink2.Object });

        var publisher = new AnyMediatorPublisher(services.Object);

        await publisher.Publish(new TestNotification());

        sink1.Verify(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Once);
        sink2.Verify(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WhenSinkThrows_StillDispatchesToRemainingSinksThenRethrows()
    {
        var failure = new InvalidOperationException("sink failed");
        var failingSink = new Mock<INotificationSink>();
        failingSink.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var healthySink = new Mock<INotificationSink>();
        healthySink.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationSink>)))
            .Returns(new[] { failingSink.Object, healthySink.Object });

        var publisher = new AnyMediatorPublisher(services.Object);

        var act = () => publisher.Publish(new TestNotification());

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        healthySink.Verify(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WhenSeveralSinksThrow_ThrowsAggregateExceptionWithEveryFailure()
    {
        var first = new InvalidOperationException("first");
        var second = new TimeoutException("second");

        var sink1 = new Mock<INotificationSink>();
        sink1.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>())).ThrowsAsync(first);
        var sink2 = new Mock<INotificationSink>();
        sink2.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>())).ThrowsAsync(second);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationSink>)))
            .Returns(new[] { sink1.Object, sink2.Object });

        var publisher = new AnyMediatorPublisher(services.Object);

        var act = () => publisher.Publish(new TestNotification());

        (await act.Should().ThrowAsync<AggregateException>())
            .Which.InnerExceptions.Should().Equal(first, second);
    }

    [Fact]
    public async Task Publish_ObjectOverload_DispatchesWithTheRuntimeType()
    {
        var sink = new Mock<INotificationSink>(MockBehavior.Strict);
        sink.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationSink>))).Returns(new[] { sink.Object });

        IPublisher publisher = new AnyMediatorPublisher(services.Object);
        object notification = new TestNotification();

        await publisher.Publish(notification);

        // Dispatch<TestNotification>, not Dispatch<INotification>: sinks keyed on typeof(TNotification) see the real type.
        sink.Verify(x => x.Dispatch(It.Is<TestNotification>(n => ReferenceEquals(n, notification)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_ObjectOverload_WhenNotANotification_ThrowsArgumentException()
    {
        IPublisher publisher = new AnyMediatorPublisher(new Mock<IServiceProvider>().Object);

        var act = () => publisher.Publish((object)"not a notification");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
