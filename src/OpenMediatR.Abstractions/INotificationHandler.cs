namespace OpenMediatR;

/// <summary>
/// Handles a notification of type <typeparamref name="TNotification"/>. Any number of handlers may exist for the
/// same notification; they are discovered by assembly scanning and executed by the configured
/// <see cref="INotificationPublisher"/>.
/// </summary>
/// <typeparam name="TNotification">The notification type handled.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the notification.
    /// </summary>
    /// <param name="notification">The published notification.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
