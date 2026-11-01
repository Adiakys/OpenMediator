namespace AnyMediator.NotificationPublishers;

/// <summary>
/// Executes notification handlers sequentially, awaiting each one before proceeding to the next.
/// This is the default strategy.
/// </summary>
public sealed class ForeachAwaitPublisher : INotificationPublisher
{
    /// <inheritdoc />
    public async Task Publish(IEnumerable<Func<Task>> handlerCalls, CancellationToken cancellationToken)
    {
        foreach (var handlerCall in handlerCalls)
            await handlerCall();
    }
}
