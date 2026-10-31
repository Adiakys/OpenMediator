---
description: "Define requests with IRequest<TResponse>, handle them with IRequestHandler and send them through ISender in OpenMediatR."
---

# Requests

A request is a message with exactly one handler. Queries return data, commands change state; OpenMediatR treats both the same way.

## Request with a response

```csharp
public sealed record GetUser(int Id) : IRequest<User>;

public sealed class GetUserHandler(IUserStore store) : IRequestHandler<GetUser, User>
{
    public Task<User> Handle(GetUser request, CancellationToken cancellationToken)
        => store.FindAsync(request.Id, cancellationToken);
}
```

```csharp
User user = await sender.Send(new GetUser(42), cancellationToken);
```

The response type is inferred from `IRequest<User>`, so `Send` needs no type argument.

## Request without a response

Implement `IRequest` and `IRequestHandler<TRequest>`. The handler returns a plain `Task`:

```csharp
public sealed record DeleteUser(int Id) : IRequest;

public sealed class DeleteUserHandler(IUserStore store) : IRequestHandler<DeleteUser>
{
    public Task Handle(DeleteUser request, CancellationToken cancellationToken)
        => store.DeleteAsync(request.Id, cancellationToken);
}
```

```csharp
await sender.Send(new DeleteUser(42), cancellationToken);
```

Internally the response type is `Unit`, a value that carries no information. Pipeline behaviors see these requests as `IPipelineBehavior<DeleteUser, Unit>`.

## Rules

- Each request type has exactly one handler. Assembly scanning keeps the first one it finds.
- If no handler is registered, `Send` throws `InvalidOperationException` with the request type and the interface to implement.
- Exceptions thrown by the handler reach the caller with their original stack trace, unless an [exception handler](./exception-handling) recovers.
- The `CancellationToken` passed to `Send` reaches every behavior, processor and the handler.

## Handler lifetime

Handlers are registered as transient. Inject scoped services such as a `DbContext` freely: in ASP.NET Core the handler is resolved from the request scope.
