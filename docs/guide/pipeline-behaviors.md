---
description: "Wrap every request with middleware such as logging, validation and transactions using IPipelineBehavior in OpenMediatR. Behaviors run in registration order."
---

# Pipeline behaviors

A pipeline behavior wraps the handling of a request, like ASP.NET Core middleware wraps an HTTP request. Use it for logic that applies to many requests: logging, validation, transactions, caching, metrics.

```csharp
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}
```

`next()` runs the rest of the pipeline and returns the response. Call it with a token (`next(cancellationToken)`) to pass a different one down; without an argument it uses `default`. Return without calling `next` to short-circuit, for example to serve a cached response.

## Register

Behaviors are not found by scanning. Register them explicitly:

```csharp
services.AddOpenMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
```

| Method | Registers |
|--------|-----------|
| `AddOpenBehavior(typeof(B<,>))` | an open generic behavior for every request |
| `AddOpenBehaviors(types)` | several open generic behaviors, in order |
| `AddBehavior<TImplementation>()` | a closed behavior, under every `IPipelineBehavior<,>` it implements |
| `AddBehavior<TService, TImplementation>()` | a closed behavior under one explicit service type |

Each method takes an optional `ServiceLifetime`. Open behaviors default to `cfg.Lifetime`; closed ones to transient.

## Order

Behaviors run in registration order. The first registered is the outermost: its code before `next()` runs first and its code after `next()` runs last. With the registration above:

```
LoggingBehavior      before next()
  ValidationBehavior before next()
    handler
  ValidationBehavior after next()
LoggingBehavior      after next()
```

OpenMediatR adds its own wrappers around yours when you use processors or exception handlers:

```
exception actions and exception handlers   outermost
your behaviors, in registration order
post-processors, then pre-processors       innermost, next to the handler
```

## Constraint

Constrain the request type parameter to `IRequest<TResponse>`, as in the example above. Behaviors written for MediatR use `where TRequest : notnull` and need this change (see [Migrating from MediatR](./migrating-from-mediatr#behavior-constraints)).

An open generic behavior must have the same two type parameters as `IPipelineBehavior<TRequest, TResponse>`, because the container closes it for each request. To target a single request type, write a closed behavior and register it with `AddBehavior<T>()`.
