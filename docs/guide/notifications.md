---
description: "Publish events to any number of handlers with INotification, INotificationHandler and IPublisher in AnyMediator, sequentially or in parallel."
---

# Notifications

A notification is an event with any number of handlers, zero included.

```csharp
public sealed record OrderPlaced(int OrderId) : INotification;

public sealed class SendConfirmation(IEmailSender email) : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
        => email.SendConfirmationAsync(notification.OrderId, cancellationToken);
}

public sealed class ReserveStock(IInventory inventory) : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
        => inventory.ReserveAsync(notification.OrderId, cancellationToken);
}
```

```csharp
await publisher.Publish(new OrderPlaced(orderId), cancellationToken);
```

Handlers are found by assembly scanning. A class may handle several notifications by implementing `INotificationHandler<T>` more than once.

## Handlers for base types and generic handlers

A handler declared for a base class or an interface also receives every notification derived from it:

```csharp
public interface IDomainEvent : INotification;

public sealed record OrderPlaced(int OrderId) : IDomainEvent;

// Runs for OrderPlaced and for every other IDomainEvent.
public sealed class DomainEventLog(ILogger<DomainEventLog> logger) : INotificationHandler<IDomainEvent>
{
    public Task Handle(IDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Event} published", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
```

Open generic handlers are found by scanning too, and are closed for each notification they match, constraints included:

```csharp
public sealed record EntityCreated<TEntity>(TEntity Entity) : INotification;

public sealed class IndexEntity<TEntity>(ISearchIndex index) : INotificationHandler<EntityCreated<TEntity>>
{
    public Task Handle(EntityCreated<TEntity> notification, CancellationToken cancellationToken)
        => index.AddAsync(notification.Entity, cancellationToken);
}
```

Handlers for the notification's own type run first, then handlers for its base classes, then handlers for its interfaces. A handler class runs at most once per `Publish`, for the most specific type it handles. Handlers for base types and generic handlers are found by assembly scanning; a handler you register by hand is used for its exact notification type.

## How handlers run

The `INotificationPublisher` strategy decides how the handlers of one notification are executed:

| Strategy | Behavior |
|----------|----------|
| `ForeachAwaitPublisher` (default) | One after another; stops at the first exception |
| `ForeachAwaitContinueOnExceptionPublisher` | One after another; runs them all, then throws an `AggregateException` with every failure |
| `TaskWhenAllPublisher` | All at once with `Task.WhenAll` |

```csharp
services.AddAnyMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
});
```

The publishers live in the `AnyMediator.NotificationPublishers` namespace. To write your own, implement `INotificationPublisher`; to pass a configured instance, set `cfg.NotificationPublisher`, which takes precedence over `NotificationPublisherType`.

## Errors

A failing handler's exception reaches the caller of `Publish`, as in MediatR. Which handlers still run depends on the strategy in the table above.

`Publish` hands the notification to every [notification sink](./notification-sinks), and a failing sink does not stop the others: once all sinks have run, the exception is re-thrown, or an `AggregateException` when more than one sink failed.

## Publishing an object

When the notification's type is only known at runtime, pass it as `object`. It must implement `INotification`, otherwise an `ArgumentException` is thrown:

```csharp
foreach (object domainEvent in aggregate.DequeueEvents())
    await publisher.Publish(domainEvent, cancellationToken);
```
