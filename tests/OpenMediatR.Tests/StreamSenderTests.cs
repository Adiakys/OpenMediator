using System.Runtime.CompilerServices;
using FluentAssertions;
using Moq;

namespace OpenMediatR.Tests;

public sealed class StreamSenderTests
{
    [Fact]
    public async Task CreateStream_WithoutPipeline_YieldsAllValuesFromHandler()
    {
        var handler = new TestStreamHandler();
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IStreamRequestHandler<TestStreamRequest, int>)))
            .Returns(handler);

        var sender = new OpenMediatRSender(services.Object);

        var values = new List<int>();
        await foreach (var value in sender.CreateStream(new TestStreamRequest(3)))
            values.Add(value);

        values.Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task CreateStream_WithStreamPipelineBehaviors_RunsAllBehaviors()
    {
        var handler = new TestStreamHandler();
        var pipeline1 = new TestStreamPipelineBehavior<TestStreamRequest, int>();
        var pipeline2 = new TestStreamPipelineBehavior<TestStreamRequest, int>();

        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IStreamRequestHandler<TestStreamRequest, int>)))
            .Returns(handler);

        var pipelineServiceType = typeof(IEnumerable<>).MakeGenericType(typeof(IStreamPipelineBehavior<TestStreamRequest, int>));
        services.Setup(x => x.GetService(pipelineServiceType))
            .Returns(new List<IStreamPipelineBehavior<TestStreamRequest, int>> { pipeline1, pipeline2 });

        var sender = new OpenMediatRSender(services.Object);

        var values = new List<int>();
        await foreach (var value in sender.CreateStream(new TestStreamRequest(2)))
            values.Add(value);

        values.Should().Equal(0, 1);
        pipeline1.Count.Should().Be(1);
        pipeline2.Count.Should().Be(1);
    }

    [Fact]
    public async Task CreateStream_PassesCancellationTokenIntoHandler()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CapturingStreamHandler();
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IStreamRequestHandler<TestStreamRequest, int>)))
            .Returns(handler);

        var sender = new OpenMediatRSender(services.Object);

        await foreach (var _ in sender.CreateStream(new TestStreamRequest(1), cts.Token)) { }

        handler.ReceivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task CreateStream_WhenHandlerThrows_PropagatesOriginalException()
    {
        var expected = new InvalidOperationException("stream failed");
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IStreamRequestHandler<TestStreamRequest, int>)))
            .Returns(new ThrowingStreamHandler(expected));

        var sender = new OpenMediatRSender(services.Object);

        var act = async () =>
        {
            await foreach (var _ in sender.CreateStream(new TestStreamRequest(3))) { }
        };

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task CreateStream_WhenCancellationRequestedMidStream_StopsEnumeration()
    {
        using var cts = new CancellationTokenSource();
        var handler = new TestStreamHandler();
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IStreamRequestHandler<TestStreamRequest, int>)))
            .Returns(handler);

        var sender = new OpenMediatRSender(services.Object);

        var values = new List<int>();
        var act = async () =>
        {
            await foreach (var value in sender.CreateStream(new TestStreamRequest(10), cts.Token))
            {
                values.Add(value);
                if (values.Count == 2) cts.Cancel();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        values.Should().Equal(0, 1);
    }

    [Fact]
    public async Task CreateStream_ObjectOverload_DispatchesToTypedHandler()
    {
        var handler = new TestStreamHandler();
        var services = new Mock<IServiceProvider>();
        services.Setup(x => x.GetService(typeof(IStreamRequestHandler<TestStreamRequest, int>)))
            .Returns(handler);

        var sender = new OpenMediatRSender(services.Object);
        object request = new TestStreamRequest(3);

        var values = new List<object?>();
        await foreach (var value in sender.CreateStream(request))
            values.Add(value);

        values.Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task CreateStream_ObjectOverload_WhenRequestIsNotIStreamRequest_Throws()
    {
        var services = new Mock<IServiceProvider>();
        var sender = new OpenMediatRSender(services.Object);

        var act = async () =>
        {
            await foreach (var _ in sender.CreateStream(new object())) { }
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateStream_ObjectOverload_WhenRequestIsNull_Throws()
    {
        var services = new Mock<IServiceProvider>();
        var sender = new OpenMediatRSender(services.Object);

        var act = async () =>
        {
            await foreach (var _ in sender.CreateStream(null!)) { }
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed class CapturingStreamHandler : IStreamRequestHandler<TestStreamRequest, int>
    {
        public CancellationToken ReceivedToken { get; private set; }

        public async IAsyncEnumerable<int> Handle(TestStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            yield return 0;
            await Task.CompletedTask;
        }
    }

    private sealed class ThrowingStreamHandler : IStreamRequestHandler<TestStreamRequest, int>
    {
        private readonly Exception _exception;
        public ThrowingStreamHandler(Exception exception) => _exception = exception;

        public async IAsyncEnumerable<int> Handle(TestStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (_exception is not null) throw _exception;
            yield break;
        }
    }
}
