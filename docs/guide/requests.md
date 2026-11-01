---
description: "Define requests with IRequest<TResponse>, handle them with IRequestHandler and send them through ISender in AnyMediator."
---

# Requests

A request is a message with exactly one handler. Queries return data, commands change state; AnyMediator treats both the same way.

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

- Each request type has exactly one handler. If scanning finds two, `AddAnyMediator` throws an `InvalidOperationException` that names both classes; remove one, or leave it out of scanning with `cfg.TypeEvaluator`.
- If no handler is registered, `Send` throws `InvalidOperationException` with the request type and the interface to implement.
- Exceptions thrown by the handler reach the caller with their original stack trace, unless an [exception handler](./exception-handling) recovers.
- The `CancellationToken` passed to `Send` reaches every behavior, processor and the handler.

## Generic handlers

An open generic handler is found by scanning and closed for each request it matches:

```csharp
public sealed record GetById<TEntity>(int Id) : IRequest<TEntity?> where TEntity : class;

public sealed class GetByIdHandler<TEntity>(AppDbContext db) : IRequestHandler<GetById<TEntity>, TEntity?>
    where TEntity : class
{
    public async Task<TEntity?> Handle(GetById<TEntity> request, CancellationToken cancellationToken)
        => await db.Set<TEntity>().FindAsync([request.Id], cancellationToken);
}
```

A handler registered for the exact request type takes precedence over a generic one.

## Sending an object

When the request's type is only known at runtime, pass it as `object`. The response comes back boxed, and is `Unit.Value` for a request without a response:

```csharp
object? response = await sender.Send(command, cancellationToken);
```

The object must implement `IRequest<TResponse>` for exactly one response type, otherwise an `ArgumentException` is thrown.

## Handler lifetime

Handlers are registered as transient. Inject scoped services such as a `DbContext` freely: in ASP.NET Core the handler is resolved from the request scope.
