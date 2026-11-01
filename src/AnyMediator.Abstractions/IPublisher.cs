namespace AnyMediator;

/// <summary>
/// Entry point for publishing notifications.
/// Fans out to all registered <see cref="INotificationSink"/> implementations sequentially.
/// Every sink runs even if an earlier one throws; the failures are then re-thrown to the caller: the exception itself
/// when one sink failed, an <see cref="AggregateException"/> when several did.
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

    /// <summary>
    /// Publishes a notification whose type is only known at runtime. Handlers are found from the runtime type of
    /// <paramref name="notification"/>, as with <see cref="Publish{TNotification}"/>.
    /// </summary>
    /// <param name="notification">The notification to publish; it must implement <see cref="INotification"/>.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <exception cref="ArgumentException"><paramref name="notification"/> does not implement <see cref="INotification"/>.</exception>
    Task Publish(object notification, CancellationToken cancellationToken = default);
}
