using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.Tests;

public sealed class TelemetryTests : IDisposable
{
    // Listeners are process-wide and tests run in parallel: each test filters on message types of its own.
    private readonly ConcurrentBag<Activity> _activities = [];
    private readonly ConcurrentBag<(double Value, Dictionary<string, object?> Tags)> _measurements = [];
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener = new();

    public TelemetryTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AnyMediatorDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _activities.Add,
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AnyMediatorDiagnostics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _meterListener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            _measurements.Add((value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
        _meterListener.Start();
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }

    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddAnyMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<TelemetryTests>());
        return services.BuildServiceProvider();
    }

    private Activity[] ActivitiesFor<TMessage>()
        => _activities.Where(a => (string?)a.GetTagItem("anymediator.message.type") == typeof(TMessage).FullName).ToArray();

    [Fact]
    public async Task Send_RecordsSpanAndDuration()
    {
        var response = await BuildProvider().GetRequiredService<ISender>().Send(new TracedRequest());

        response.Should().Be("traced");
        var span = ActivitiesFor<TracedRequest>().Should().ContainSingle().Subject;
        span.DisplayName.Should().Be("send TracedRequest");
        span.GetTagItem("anymediator.operation").Should().Be("send");
        span.Status.Should().Be(ActivityStatusCode.Unset);

        _measurements.Should().Contain(m =>
            Equals(m.Tags["anymediator.message.type"], typeof(TracedRequest).FullName)
            && Equals(m.Tags["anymediator.operation"], "send")
            && !m.Tags.ContainsKey("error.type"));
    }

    [Fact]
    public async Task Send_WhenHandlerThrows_RecordsErrorOnSpanAndDuration()
    {
        var act = () => BuildProvider().GetRequiredService<ISender>().Send(new FailingTracedRequest());

        await act.Should().ThrowAsync<TimeoutException>();
        var span = ActivitiesFor<FailingTracedRequest>().Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.GetTagItem("error.type").Should().Be(typeof(TimeoutException).FullName);
        span.Events.Should().ContainSingle(e => e.Name == "exception");

        _measurements.Should().Contain(m =>
            Equals(m.Tags["anymediator.message.type"], typeof(FailingTracedRequest).FullName)
            && Equals(m.Tags["error.type"], typeof(TimeoutException).FullName));
    }

    [Fact]
    public async Task Publish_RecordsPublishSpanWithOneChildPerHandler()
    {
        await BuildProvider().GetRequiredService<IPublisher>().Publish(new TracedEvent());

        var spans = ActivitiesFor<TracedEvent>();
        var publish = spans.Should().ContainSingle(a => a.DisplayName == "publish TracedEvent").Subject;
        var handlers = spans.Where(a => a.DisplayName == "handle TracedEvent").ToArray();

        handlers.Should().HaveCount(2).And.OnlyContain(h => h.ParentSpanId == publish.SpanId);
        handlers.Select(h => h.GetTagItem("anymediator.handler.type"))
            .Should().BeEquivalentTo(new[] { typeof(FirstTracedEventHandler).FullName, typeof(SecondTracedEventHandler).FullName });
    }

    [Fact]
    public async Task CreateStream_SpanLastsUntilEnumerationEnds()
    {
        var items = new List<int>();
        await foreach (var item in BuildProvider().GetRequiredService<ISender>().CreateStream(new TracedStreamRequest()))
        {
            items.Add(item);
            ActivitiesFor<TracedStreamRequest>().Should().BeEmpty("the span is still open while items are read");
        }

        items.Should().Equal(1, 2);
        ActivitiesFor<TracedStreamRequest>().Should().ContainSingle().Which.DisplayName.Should().Be("create_stream TracedStreamRequest");
    }

    private sealed record TracedRequest : IRequest<string>;

    private sealed class TracedRequestHandler : IRequestHandler<TracedRequest, string>
    {
        public Task<string> Handle(TracedRequest request, CancellationToken cancellationToken) => Task.FromResult("traced");
    }

    private sealed record FailingTracedRequest : IRequest<string>;

    private sealed class FailingTracedRequestHandler : IRequestHandler<FailingTracedRequest, string>
    {
        public Task<string> Handle(FailingTracedRequest request, CancellationToken cancellationToken) => throw new TimeoutException("too slow");
    }

    private sealed record TracedEvent : INotification;

    private sealed class FirstTracedEventHandler : INotificationHandler<TracedEvent>
    {
        public Task Handle(TracedEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SecondTracedEventHandler : INotificationHandler<TracedEvent>
    {
        public Task Handle(TracedEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record TracedStreamRequest : IStreamRequest<int>;

    private sealed class TracedStreamHandler : IStreamRequestHandler<TracedStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(TracedStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return 1;
            await Task.Yield();
            yield return 2;
        }
    }
}
