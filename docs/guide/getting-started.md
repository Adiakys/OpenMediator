---
description: "Install AnyMediator from NuGet, register it with AddAnyMediator and send your first request through ISender."
---

# Getting started

AnyMediator is an in-process mediator for .NET 8 and .NET 10. A caller sends a message to `ISender` or `IPublisher`; AnyMediator finds the handler in the dependency injection container and runs it, through any pipeline behaviors you registered.

## Install

```bash
dotnet add package AnyMediator
```

The package brings in `AnyMediator.Abstractions`, which holds only the interfaces. Class libraries that define requests and handlers can reference `AnyMediator.Abstractions` alone.

## Register

Call `AddAnyMediator` on your `IServiceCollection` and tell it which assemblies to scan:

```csharp
using AnyMediator;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAnyMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
```

The scan registers every non-abstract, non-generic class that implements:

| Interface | Registered as |
|-----------|---------------|
| `IRequestHandler<TRequest, TResponse>` | one handler per request type |
| `IStreamRequestHandler<TRequest, TResponse>` | one handler per stream request type |
| `INotificationHandler<TNotification>` | any number per notification type |
| `IRequestExceptionHandler<TRequest, TResponse, TException>` | any number |
| `IRequestExceptionAction<TRequest, TException>` | any number |

Pipeline behaviors are never scanned: add them with `AddOpenBehavior`. Pre- and post-processors are scanned only when `AutoRegisterRequestProcessors` is `true`. See [Configuration](./configuration).

## Send a request

```csharp
public sealed record GetUser(int Id) : IRequest<User>;

public sealed class GetUserHandler(IUserStore store) : IRequestHandler<GetUser, User>
{
    public Task<User> Handle(GetUser request, CancellationToken cancellationToken)
        => store.FindAsync(request.Id, cancellationToken);
}
```

Inject `ISender` wherever you need to dispatch:

```csharp
app.MapGet("/users/{id}", (int id, ISender sender, CancellationToken ct) =>
    sender.Send(new GetUser(id), ct));
```

## What to inject

| Service | Use it to |
|---------|-----------|
| `ISender` | send requests (`Send`) and open streams (`CreateStream`) |
| `IPublisher` | publish notifications (`Publish`) |
| `IMediator` | both, when a class really needs both |

Inject the narrowest one: a class that only publishes events should not be able to send commands.

## Next steps

- Coming from MediatR? Read [Migrating from MediatR](./migrating-from-mediatr).
- Add cross-cutting logic with [pipeline behaviors](./pipeline-behaviors).
- Publish events with [notifications](./notifications).
