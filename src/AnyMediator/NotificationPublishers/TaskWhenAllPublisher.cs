namespace AnyMediator.NotificationPublishers;

/// <summary>
/// Executes all notification handlers in parallel using Task.WhenAll.
/// </summary>
public sealed class TaskWhenAllPublisher : INotificationPublisher
{
    /// <inheritdoc />
    public Task Publish(IEnumerable<Func<Task>> handlerCalls, CancellationToken cancellationToken)
        => Task.WhenAll(handlerCalls.Select(call => call()));
}
