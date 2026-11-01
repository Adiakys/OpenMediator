using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace AnyMediator;

/// <summary>
/// Spans and duration measurements for mediator operations. Callers check <see cref="IsEnabled"/> first, so with no
/// listener the dispatch path pays one check and nothing else. Operations take their inputs as a state value and a
/// static lambda, so tracing allocates no closure.
/// </summary>
internal static class Telemetry
{
    private static readonly string? Version = typeof(Telemetry).Assembly.GetName().Version?.ToString(3);
    internal static readonly ActivitySource Source = new(AnyMediatorDiagnostics.ActivitySourceName, Version);
    private static readonly Meter Meter = new(AnyMediatorDiagnostics.MeterName, Version);

    internal static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "anymediator.operation.duration", "s", "Duration of a mediator operation, behaviors and handlers included.");

    internal static bool IsEnabled => Source.HasListeners() || Duration.Enabled;

    internal static async Task<T> Run<TState, T>(OperationSpan span, TState state, Func<TState, Task<T>> operation)
    {
        using var activity = span.Start();
        var start = Stopwatch.GetTimestamp();
        Exception? failure = null;
        try
        {
            return await operation(state);
        }
        catch (Exception ex)
        {
            RecordFailure(activity, failure = ex);
            throw;
        }
        finally
        {
            span.RecordDuration(start, failure);
        }
    }

    internal static async Task Run<TState>(OperationSpan span, TState state, Func<TState, Task> operation)
    {
        using var activity = span.Start();
        var start = Stopwatch.GetTimestamp();
        Exception? failure = null;
        try
        {
            await operation(state);
        }
        catch (Exception ex)
        {
            RecordFailure(activity, failure = ex);
            throw;
        }
        finally
        {
            span.RecordDuration(start, failure);
        }
    }

    /// <summary>A span that lasts until the stream is fully enumerated, fails or is abandoned.</summary>
    internal static async IAsyncEnumerable<T> RunStream<TState, T>(
        OperationSpan span, TState state, Func<TState, IAsyncEnumerable<T>> operation, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = span.Start();
        var start = Stopwatch.GetTimestamp();
        Exception? failure = null;

        await using var items = operation(state).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool hasItem;
                try
                {
                    hasItem = await items.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    RecordFailure(activity, failure = ex);
                    throw;
                }

                if (!hasItem) yield break;
                yield return items.Current;
            }
        }
        finally
        {
            span.RecordDuration(start, failure);
        }
    }

    // Follows the OpenTelemetry conventions for exceptions: error status, error.type, and an "exception" event.
    private static void RecordFailure(Activity? activity, Exception exception)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error.type", exception.GetType().FullName);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            ["exception.type"] = exception.GetType().FullName,
            ["exception.message"] = exception.Message,
            ["exception.stacktrace"] = exception.ToString(),
        }));
    }
}

/// <summary>
/// The name and attributes of one kind of span, such as "send GetUser", built once and reused by every operation of
/// that kind.
/// </summary>
internal sealed class OperationSpan(string operation, Type messageType, Type? handlerType = null)
{
    private readonly string _name = $"{operation} {messageType.Name}";

    private readonly KeyValuePair<string, object?>[] _metricTags =
    [
        new("anymediator.operation", operation),
        new("anymediator.message.type", messageType.FullName),
    ];

    private readonly KeyValuePair<string, object?>[] _spanTags = handlerType is null
        ? [new("anymediator.operation", operation), new("anymediator.message.type", messageType.FullName)]
        : [new("anymediator.operation", operation), new("anymediator.message.type", messageType.FullName), new("anymediator.handler.type", handlerType.FullName)];

    // A default parent context makes the span a child of Activity.Current, as StartActivity(name) does.
    public Activity? Start() => Telemetry.Source.StartActivity(_name, ActivityKind.Internal, default(ActivityContext), _spanTags);

    public void RecordDuration(long start, Exception? failure)
    {
        if (!Telemetry.Duration.Enabled) return;

        var seconds = Stopwatch.GetElapsedTime(start).TotalSeconds;
        if (failure is null)
            Telemetry.Duration.Record(seconds, _metricTags);
        else
            Telemetry.Duration.Record(seconds, [.. _metricTags, new("error.type", failure.GetType().FullName)]);
    }
}
