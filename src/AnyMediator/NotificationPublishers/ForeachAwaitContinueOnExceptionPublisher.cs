namespace AnyMediator.NotificationPublishers;

/// <summary>
/// Executes notification handlers sequentially. Exceptions thrown by individual handlers
/// are captured and, after all handlers have run, surfaced together as a single
/// <see cref="AggregateException"/>. If no handler throws, completes normally.
/// </summary>
public sealed class ForeachAwaitContinueOnExceptionPublisher : INotificationPublisher
{
    /// <inheritdoc />
    public async Task Publish(IEnumerable<Func<Task>> handlerCalls, CancellationToken cancellationToken)
    {
        List<Exception>? exceptions = null;

        foreach (var handlerCall in handlerCalls)
        {
            try
            {
                await handlerCall();
            }
            catch (Exception ex)
            {
                (exceptions ??= []).Add(ex);
            }
        }

        if (exceptions is not null)
            throw new AggregateException(exceptions);
    }
}
