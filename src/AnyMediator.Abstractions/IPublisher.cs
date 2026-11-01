namespace AnyMediator;

/// <summary>
/// Entry point for publishing notifications.
/// Fans out to all registered <see cref="INotificationSink"/> implementations sequentially.
/// All sinks are executed even if one throws; exceptions are logged and do not propagate.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publishes a notification to every registered <see cref="INotificationSink"/>; the default in-memory sink
    /// invokes all <see cref="INotificationHandler{TNotification}"/> implementations for it.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
