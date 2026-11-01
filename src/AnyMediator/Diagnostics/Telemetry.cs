using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace AnyMediator;

/// <summary>
/// Spans and duration measurements for mediator operations. Callers check <see cref="IsEnabled"/> first, so with no
/// listener the dispatch path pays one check and nothing else.
/// </summary>
internal static class Telemetry
{
    private static readonly string? Version = typeof(Telemetry).Assembly.GetName().Version?.ToString(3);
    private static readonly ActivitySource Source = new(AnyMediatorDiagnostics.ActivitySourceName, Version);
    private static readonly Meter Meter = new(AnyMediatorDiagnostics.MeterName, Version);

    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "anymediator.operation.duration", "s", "Duration of a mediator operation, behaviors and handlers included.");

    internal static bool IsEnabled => Source.HasListeners() || Duration.Enabled;

    /// <summary>Runs <paramref name="operation"/> inside a span named "<paramref name="name"/> {message type}" and measures it.</summary>
    internal static async Task<T> Run<T>(string name, Type messageType, Func<Task<T>> operation, Type? handlerType = null)
    {
        using var activity = Start(name, messageType, handlerType);
        var start = Stopwatch.GetTimestamp();
        Exception? failure = null;
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            RecordFailure(activity, failure = ex);
            throw;
        }
        finally
        {
            Record(name, messageType, start, failure);
        }
    }

    internal static Task Run(string name, Type messageType, Func<Task> operation, Type? handlerType = null)
        => Run(name, messageType, async () => { await operation(); return true; }, handlerType);

    /// <summary>A span that lasts until the stream is fully enumerated, fails or is abandoned.</summary>
    internal static async IAsyncEnumerable<T> RunStream<T>(
        string name, Type messageType, Func<IAsyncEnumerable<T>> operation, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = Start(name, messageType, null);
        var start = Stopwatch.GetTimestamp();
        Exception? failure = null;

        await using var items = operation().GetAsyncEnumerator(cancellationToken);
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
            Record(name, messageType, start, failure);
        }
    }

    private static Activity? Start(string name, Type messageType, Type? handlerType)
    {
        var activity = Source.StartActivity($"{name} {messageType.Name}");
        activity?.SetTag("anymediator.operation", name);
        activity?.SetTag("anymediator.message.type", messageType.FullName);
        if (handlerType is not null)
            activity?.SetTag("anymediator.handler.type", handlerType.FullName);
        return activity;
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

    private static void Record(string name, Type messageType, long start, Exception? exception)
    {
        if (!Duration.Enabled) return;

        var tags = new TagList
        {
            { "anymediator.operation", name },
            { "anymediator.message.type", messageType.FullName },
        };
        if (exception is not null)
            tags.Add("error.type", exception.GetType().FullName);

        Duration.Record(Stopwatch.GetElapsedTime(start).TotalSeconds, tags);
    }
}
