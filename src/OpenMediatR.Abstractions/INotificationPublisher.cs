namespace OpenMediatR;

/// <summary>
/// Defines the strategy for executing notification handlers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Executes the handler calls for a single notification.
    /// </summary>
    /// <param name="handlerCalls">One delegate per resolved handler; invoking it runs that handler.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task Publish(IEnumerable<Func<Task>> handlerCalls, CancellationToken cancellationToken);
}
