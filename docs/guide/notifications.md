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

`Publish` hands the notification to every [notification sink](./notification-sinks). A failure inside a sink, including a failing handler in the default in-memory sink, is logged with `ILogger` and does not reach the caller. The other sinks still run.

This differs from MediatR, where `Publish` re-throws. If the caller must react to a failure, model that step as a [request](./requests).
