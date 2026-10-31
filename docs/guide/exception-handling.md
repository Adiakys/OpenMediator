---
description: "Recover from exceptions with IRequestExceptionHandler or log them with IRequestExceptionAction in OpenMediatR, with base-type matching."
---

# Exception handling

Two interfaces handle exceptions thrown anywhere in a request's pipeline: by the handler, a processor or a behavior.

| Interface | Can recover | Use it for |
|-----------|-------------|------------|
| `IRequestExceptionHandler<TRequest, TResponse, TException>` | Yes | returning a fallback response |
| `IRequestExceptionAction<TRequest, TException>` | No | logging, metrics, alerts |

## Recover with a fallback

```csharp
public sealed class UserStoreUnavailable
    : IRequestExceptionHandler<GetUser, User, TimeoutException>
{
    public Task Handle(
        GetUser request,
        TimeoutException exception,
        RequestExceptionHandlerState<User> state,
        CancellationToken cancellationToken)
    {
        state.SetHandled(User.Unknown(request.Id));
        return Task.CompletedTask;
    }
}
```

Calling `SetHandled` stops the exception: the caller of `Send` receives the fallback. If no handler calls it, the exception keeps propagating with its original stack trace.

## Act without recovering

```csharp
public sealed class LogGetUserFailure(ILogger<LogGetUserFailure> logger)
    : IRequestExceptionAction<GetUser, Exception>
{
    public Task Execute(GetUser request, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "GetUser({Id}) failed", request.Id);
        return Task.CompletedTask;
    }
}
```

After every matching action has run, the original exception is re-thrown.

## Matching

- A handler or action declared for a base exception type also catches derived ones: `Exception` matches everything.
- Several handlers for the same request and exception run in registration order; the first one that calls `SetHandled` ends the search.
- Every matching action runs.

## Registration

Exception handlers and actions are found by the assembly scan with no extra setting. They are closed types, one per request type: open generic handlers and actions are not supported. You can also register them explicitly:

```csharp
cfg.AddRequestExceptionHandler<UserStoreUnavailable>();
cfg.AddRequestExceptionAction<LogGetUserFailure>();
```

## Handlers first, or actions first

`RequestExceptionActionProcessorStrategy` decides whether actions see exceptions that a handler recovers from:

| Value | Actions run |
|-------|-------------|
| `ApplyForUnhandledExceptions` (default) | only when no handler recovered |
| `ApplyForAllExceptions` | for every exception, before handlers try to recover |

```csharp
cfg.RequestExceptionActionProcessorStrategy = RequestExceptionActionProcessorStrategy.ApplyForAllExceptions;
```
