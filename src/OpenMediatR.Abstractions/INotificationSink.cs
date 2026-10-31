namespace OpenMediatR;

/// <summary>
/// Represents a delivery channel for notifications.
/// Each sink implementation determines how notifications are dispatched
/// (e.g., in-memory handlers, HTTP webhooks, message brokers).
/// </summary>
public interface INotificationSink
{
    /// <summary>
    /// Delivers the notification through this sink.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The published notification.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task Dispatch<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
