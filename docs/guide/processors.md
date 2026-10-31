---
description: "Run code just before or just after a request handler with IRequestPreProcessor and IRequestPostProcessor in OpenMediatR."
---

# Pre- and post-processors

Processors are lighter than behaviors: they do not wrap the handler, they run next to it.

- `IRequestPreProcessor<TRequest>` runs before the handler.
- `IRequestPostProcessor<TRequest, TResponse>` runs after the handler returns successfully, and receives the response.

```csharp
public sealed class ValidateUserId : IRequestPreProcessor<GetUser>
{
    public Task Process(GetUser request, CancellationToken cancellationToken)
    {
        if (request.Id <= 0) throw new ArgumentOutOfRangeException(nameof(request.Id));
        return Task.CompletedTask;
    }
}

public sealed class AuditUserRead(IAuditLog audit) : IRequestPostProcessor<GetUser, User>
{
    public Task Process(GetUser request, User response, CancellationToken cancellationToken)
        => audit.RecordAsync($"read user {response.Id}", cancellationToken);
}
```

## Register

Processors are opt-in. Either register them one by one:

```csharp
services.AddOpenMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddRequestPreProcessor<ValidateUserId>();
    cfg.AddRequestPostProcessor<AuditUserRead>();
});
```

or let the assembly scan find them:

```csharp
cfg.AutoRegisterRequestProcessors = true;
```

Open generic processors that apply to every request are registered with `AddOpenRequestPreProcessor(typeof(P<>))` and `AddOpenRequestPostProcessor(typeof(P<,>))`.

## When they run

- Several processors for the same request run one after another, in registration order.
- Pre-processors run inside your pipeline behaviors, just before the handler.
- Post-processors run just after the handler, before your behaviors see the response.
- Post-processors are skipped when the handler or a pre-processor throws.
