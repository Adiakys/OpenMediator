# OpenMediatR

A lightweight, free, in-process mediator for .NET. Implements the mediator pattern with support for requests, notifications, pipeline behaviors, and pluggable notification sinks.

## Installation

```
dotnet add package OpenMediatR
```

## Setup

Register OpenMediatR in your DI container:

```csharp
services.AddOpenMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<MyRequestHandler>();
});
```

This scans the specified assembly for `IRequestHandler<,>` and `INotificationHandler<>` implementations and registers them automatically.

## Requests

Define a request and its handler:

```csharp
public sealed record GetUser(int Id) : IRequest<User>;

public sealed class GetUserHandler : IRequestHandler<GetUser, User>
{
    public Task<User> Handle(GetUser request, CancellationToken cancellationToken)
    {
        // resolve and return the user
    }
}
```

Dispatch it via `ISender`:

```csharp
public class UserController(ISender sender)
{
    public async Task<User> Get(int id)
    {
        return await sender.Send(new GetUser(id));
    }
}
```

Each request type must have exactly one handler. If no handler is registered, `Send` throws.

For commands that return no value, use `IRequest` (without type parameter):

```csharp
public sealed record DeleteUser(int Id) : IRequest;

public sealed class DeleteUserHandler : IRequestHandler<DeleteUser>
{
    public Task Handle(DeleteUser request, CancellationToken cancellationToken)
    {
        // delete the user
        return Task.CompletedTask;
    }
}
```

```csharp
await sender.Send(new DeleteUser(id));
```

## Stream Requests

For handlers that produce a sequence of results over time (e.g. LLM token streams, paged queries, server-sent events), use `IStreamRequest<TResponse>`:

```csharp
public sealed record StreamLogs(string Source) : IStreamRequest<string>;

public sealed class StreamLogsHandler : IStreamRequestHandler<StreamLogs, string>
{
    public async IAsyncEnumerable<string> Handle(
        StreamLogs request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in ReadLines(request.Source, cancellationToken))
            yield return line;
    }
}
```

Dispatch via `ISender` (or `IMediator`):

```csharp
public class LogsController(ISender sender)
{
    public async IAsyncEnumerable<string> Tail(string source, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var line in sender.CreateStream(new StreamLogs(source), ct))
            yield return line;
    }
}
```

Stream pipeline behaviors use `IStreamPipelineBehavior<TRequest, TResponse>` and are registered with `cfg.AddOpenStreamBehavior(typeof(MyStreamBehavior<,>))`. They are independent from regular `IPipelineBehavior` — request and stream pipelines do not share behaviors.

## Notifications

Define a notification and one or more handlers:

```csharp
public sealed record OrderPlaced(int OrderId) : INotification;

public sealed class SendConfirmationEmail : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        // send email
        return Task.CompletedTask;
    }
}

public sealed class UpdateInventory : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        // update stock
        return Task.CompletedTask;
    }
}
```

Publish via `IPublisher`:

```csharp
await publisher.Publish(new OrderPlaced(orderId));
```

All registered handlers for the notification type are executed. Multiple handlers per notification type are supported.

## Pipeline Behaviors

Pipeline behaviors wrap request handling, similar to middleware. They execute in registration order.

```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var response = await next();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

Register behaviors explicitly via `AddOpenBehavior`:

```csharp
services.AddOpenMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<MyRequestHandler>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
```

Behaviors are not auto-scanned. They must be open generic types implementing `IPipelineBehavior<,>`.

## Pre / Post Processors

For logic that must run before or after a request handler without writing a full pipeline behavior, implement `IRequestPreProcessor<TRequest>` or `IRequestPostProcessor<TRequest, TResponse>`:

```csharp
public sealed class ValidateUser : IRequestPreProcessor<GetUser>
{
    public Task Process(GetUser request, CancellationToken cancellationToken)
    {
        if (request.Id <= 0) throw new ArgumentException("Invalid user id.");
        return Task.CompletedTask;
    }
}

public sealed class AuditUserAccess : IRequestPostProcessor<GetUser, User>
{
    public Task Process(GetUser request, User response, CancellationToken cancellationToken)
    {
        // record the access
        return Task.CompletedTask;
    }
}
```

Multiple processors per request type are supported and execute in registration order. Pre-processors run before the handler. Post-processors run after the handler returns successfully — they are skipped if the handler or any preceding step throws. User pipeline behaviors registered via `AddOpenBehavior` wrap the processors (outermost), so behavior `before` logic runs before pre-processors and behavior `after` logic runs after post-processors.

There are two ways to register processors:

```csharp
services.AddOpenMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<MyRequestHandler>();

    // Option 1 — explicit, recommended when you want full control:
    cfg.AddRequestPreProcessor<ValidateUser>();
    cfg.AddRequestPostProcessor<AuditUserAccess>();

    // Option 2 — opt-in assembly scan (mirrors MediatR):
    // cfg.AutoRegisterRequestProcessors = true;
});
```

Both paths trigger auto-registration of the matching pipeline-behavior wrappers (`RequestPreProcessorBehavior<,>`, `RequestPostProcessorBehavior<,>`). Without one of them, no processor logic is wired in.

## Exception Handlers / Actions

Handle exceptions thrown by a request handler — or by any pipeline step — without `try`/`catch` blocks scattered through your code. Two complementary contracts:

- `IRequestExceptionHandler<TRequest, TResponse, TException>` — can **recover** from the exception by setting a fallback response.
- `IRequestExceptionAction<TRequest, TException>` — runs **side-effects** (logging, metrics, ...) without recovery; the exception is always re-thrown after all matching actions complete.

```csharp
public sealed class FallbackOnConnectionLost : IRequestExceptionHandler<GetUser, User, ConnectionException>
{
    public Task Handle(
        GetUser request,
        ConnectionException exception,
        RequestExceptionHandlerState<User> state,
        CancellationToken cancellationToken)
    {
        state.SetHandled(User.Anonymous); // recover
        return Task.CompletedTask;
    }
}

public sealed class LogFailures : IRequestExceptionAction<GetUser, Exception>
{
    private readonly ILogger<LogFailures> _logger;
    public LogFailures(ILogger<LogFailures> logger) => _logger = logger;

    public Task Execute(GetUser request, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "GetUser({Id}) failed", request.Id);
        return Task.CompletedTask;
    }
}
```

Both are picked up automatically by the assembly scan — no flag required. Or register them explicitly:

```csharp
services.AddOpenMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<MyRequestHandler>();
    cfg.AddRequestExceptionHandler<FallbackOnConnectionLost>();
    cfg.AddRequestExceptionAction<LogFailures>();
});
```

**Type-hierarchy matching**: a handler/action declared for a base exception type catches derived ones. `IRequestExceptionAction<MyRequest, Exception>` runs for every exception thrown during `MyRequest` processing.

**Order**: when an exception bubbles up, the exception-handler wrapper runs first. If a handler calls `SetHandled`, the response is returned and the action wrapper never sees the exception. If no handler recovers, the action wrapper runs every matching action and then re-throws the original exception (preserving stack trace).

Multiple handlers per (request, exception) pair are supported; iteration stops as soon as one handler marks the state as handled. Multiple actions all run.

## Notification Sinks

Notifications are dispatched through notification sinks. A sink determines how notifications reach their handlers.

The built-in `InMemoryNotificationSink` resolves `INotificationHandler<T>` from DI and invokes them in-process. It is registered by default.

You can add custom sinks for other delivery channels (message brokers, webhooks, etc.) by implementing `INotificationSink`:

```csharp
public sealed class WebhookNotificationSink : INotificationSink
{
    public async Task Dispatch<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification
    {
        // forward to external webhook
    }
}
```

Register it in DI:

```csharp
services.AddSingleton<INotificationSink, WebhookNotificationSink>();
```

The publisher fans out to all registered sinks sequentially. If a sink throws, the exception is logged and remaining sinks still execute.

## IMediator

`IMediator` combines both `ISender` and `IPublisher` into a single interface:

```csharp
public class OrderService(IMediator mediator)
{
    public async Task PlaceOrder(Order order)
    {
        var result = await mediator.Send(new CreateOrder(order));
        await mediator.Publish(new OrderPlaced(result.Id));
    }
}
```

You can inject `ISender`, `IPublisher`, or `IMediator` depending on what the consumer needs.

## Configuration

```csharp
services.AddOpenMediatR(cfg =>
{
    // Scan multiple assemblies
    cfg.RegisterServicesFromAssembly(typeof(HandlerA).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(HandlerB).Assembly);
    // or: cfg.RegisterServicesFromAssemblies(assembly1, assembly2);

    // Add pipeline behaviors (in execution order)
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));

    // Override behavior lifetime
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>), ServiceLifetime.Singleton);

    // Set service lifetime for core services (default: Transient)
    cfg.Lifetime = ServiceLifetime.Scoped;

    // Notification publishing strategy (default: ForeachAwaitPublisher — sequential)
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher); // parallel
});
```

## License

[Apache-2.0](LICENSE)
