using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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

        var publisher = new AnyMediatorPublisher(services.Object, NullLogger<AnyMediatorPublisher>.Instance);

        await publisher.Publish(new TestNotification());

        sink1.Verify(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Once);
        sink2.Verify(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WhenSinkThrows_ShouldStillDispatchToRemainingSinks()
    {
        var failingSink = new Mock<INotificationSink>();
        failingSink.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sink failed"));

        var healthySink = new Mock<INotificationSink>();
        healthySink.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationSink>)))
            .Returns(new[] { failingSink.Object, healthySink.Object });

        var publisher = new AnyMediatorPublisher(services.Object, NullLogger<AnyMediatorPublisher>.Instance);

        await publisher.Publish(new TestNotification());

        healthySink.Verify(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WhenSinkThrows_ShouldNotPropagateException()
    {
        var failingSink = new Mock<INotificationSink>();
        failingSink.Setup(x => x.Dispatch(It.IsAny<TestNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sink failed"));

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IEnumerable<INotificationSink>)))
            .Returns(new[] { failingSink.Object });

        var publisher = new AnyMediatorPublisher(services.Object, NullLogger<AnyMediatorPublisher>.Instance);

        var act = () => publisher.Publish(new TestNotification());

        await act.Should().NotThrowAsync();
    }
}
