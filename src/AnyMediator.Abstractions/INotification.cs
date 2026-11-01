namespace AnyMediator;

/// <summary>
/// Marker for a notification: a message published through <see cref="IPublisher.Publish{TNotification}"/>
/// and delivered to zero or more <see cref="INotificationHandler{TNotification}"/> implementations.
/// </summary>
public interface INotification
{
}
