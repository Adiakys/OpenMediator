---
description: "Deliver AnyMediator notifications beyond in-process handlers, to message brokers or webhooks, by implementing INotificationSink."
---

# Notification sinks

A sink is a delivery channel for notifications. `IPublisher.Publish` passes each notification to every registered `INotificationSink`, one after another.

The default sink, registered by `AddAnyMediator`, resolves the `INotificationHandler<T>` implementations from the container and runs them with the configured [notification publisher](./notifications#how-handlers-run).

## Add a channel

Implement `INotificationSink` to send notifications somewhere else, for example a message broker:

```csharp
public sealed class ServiceBusSink(ServiceBusSender sender) : INotificationSink
{
    public Task Dispatch<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(notification))
        {
            Subject = typeof(TNotification).Name,
        };
        return sender.SendMessageAsync(message, cancellationToken);
    }
}
```

Register it next to the default one:

```csharp
services.AddAnyMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
services.AddSingleton<INotificationSink, ServiceBusSink>();
```

Now every `Publish` runs the in-process handlers and also sends the message.

## Failures

Sinks are isolated from each other. If one throws, `Publish` logs the exception with the sink and notification type names, then moves on to the next sink. `Publish` itself does not throw.

## Filtering

A sink receives every notification. To forward only some of them, check the type, or mark the notifications to forward with an interface of your own:

```csharp
public interface IIntegrationEvent : INotification;

public Task Dispatch<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
    => notification is IIntegrationEvent ? Send(notification, cancellationToken) : Task.CompletedTask;
```
