---
description: "Trace every Send, CreateStream, Publish and notification handler with OpenTelemetry, and measure their duration, using AnyMediator's built-in ActivitySource and Meter."
---

# Tracing and metrics

AnyMediator emits OpenTelemetry-compatible spans and a duration metric through .NET's own `ActivitySource` and `Meter`. There is no extra package to install and no option to turn on: subscribe to the names in `AnyMediatorDiagnostics`, and nothing is recorded until something does.

## Set up OpenTelemetry

```csharp
using AnyMediator;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(AnyMediatorDiagnostics.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(AnyMediatorDiagnostics.MeterName)
        .AddOtlpExporter());
```

A request to `GET /users/42` then shows up as one trace: the ASP.NET Core span, the `send GetUser` span under it, and the database spans under that.

## Spans

| Operation | Span name | Ends |
|-----------|-----------|------|
| `Send` | `send {RequestType}` | when the handler and every behavior have finished |
| `CreateStream` | `create_stream {RequestType}` | when the stream is fully read, fails, or is abandoned |
| `Publish` | `publish {NotificationType}` | when every sink has finished |
| each notification handler | `handle {NotificationType}`, child of the publish span | when the handler has finished |

Attributes:

| Attribute | Value |
|-----------|-------|
| `anymediator.operation` | `send`, `create_stream`, `publish` or `handle` |
| `anymediator.message.type` | full name of the request or notification type |
| `anymediator.handler.type` | full name of the notification handler, on `handle` spans |
| `error.type` | full name of the exception, when the operation failed |

A failed operation sets the span status to `Error` and adds an `exception` event with `exception.type`, `exception.message` and `exception.stacktrace`, as the OpenTelemetry conventions describe. Spans started by your handlers, such as database or HTTP calls, become children of the AnyMediator span.

## Metric

| Name | Type | Unit | Attributes |
|------|------|------|------------|
| `anymediator.operation.duration` | histogram | seconds | `anymediator.operation`, `anymediator.message.type`, `error.type` when failed |

The histogram also gives the count of operations and of failures, per message type: for example the rate of `send` operations whose `error.type` is set.

## Cost

With no listener, each operation checks two flags and continues: the [benchmarks](./performance) are unchanged. With a listener that records everything, as an OpenTelemetry SDK sampling every request does, each span costs about half a microsecond, mostly the `Activity` object that any instrumented library creates:

| Operation | Not listening | Listening |
|-----------|--------------:|----------:|
| `Send` (1 span) | 89 ns, 96 B | 594 ns, 704 B |
| `Publish` with 3 handlers (4 spans) | 153 ns, 144 B | 2,374 ns, 2,776 B |

Next to a database query or an HTTP call this is not measurable. With a sampling rate below 100%, requests that are not sampled create no span and only record their duration.

## Across services

A [notification sink](./notification-sinks) that forwards notifications to a message broker can carry the trace along, so the consumer's spans join the same trace. Add the current trace context to the message:

```csharp
public Task Dispatch<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
{
    var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(notification)) { Subject = typeof(TNotification).Name };

    // W3C trace context of the publish span, read back by the consumer to continue the trace.
    if (Activity.Current is { } activity)
        message.ApplicationProperties["traceparent"] = activity.Id;

    return sender.SendMessageAsync(message, cancellationToken);
}
```

Many broker clients, the Azure Service Bus and RabbitMQ ones among them, have OpenTelemetry instrumentation that does this for you; then the sink needs no code at all.
