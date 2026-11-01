using AnyMediator.NotificationPublishers;

namespace AnyMediator.NotificationSinks;

internal sealed class InMemoryNotificationSink(IServiceProvider services, HandlerRegistry registry, INotificationPublisher publisher) : INotificationSink
{
    public Task Dispatch<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        var targets = registry.GetNotificationTargets(notification!.GetType());

        // Only handlers of the notification's own type, the usual case: nothing to deduplicate. With the default
        // strategy and no tracing the handlers are awaited directly, without a delegate per handler.
        if (targets.Length == 1)
            return publisher is ForeachAwaitPublisher && !Telemetry.IsEnabled
                ? targets[0].HandleInOrder(services, notification, cancellationToken)
                : publisher.Publish(targets[0].CreateCalls(services, notification, cancellationToken), cancellationToken);

        // With handlers for base types too, a class that already ran for a more specific type is skipped, so a class
        // handling both OrderPlaced and IDomainEvent runs once; several instances registered for one type all run.
        var handlerCalls = new List<Func<Task>>();
        var classesDone = new HashSet<Type>();

        foreach (var target in targets)
        {
            var classesOfTarget = new HashSet<Type>();

            foreach (var handler in target.ResolveHandlers(services))
            {
                var handlerClass = handler.GetType();
                if (handlerClass.IsGenericType)
                    handlerClass = handlerClass.GetGenericTypeDefinition();

                if (classesDone.Contains(handlerClass))
                    continue;

                classesOfTarget.Add(handlerClass);
                handlerCalls.Add(target.CreateCall(handler, notification, cancellationToken));
            }

            classesDone.UnionWith(classesOfTarget);
        }

        return publisher.Publish(handlerCalls, cancellationToken);
    }
}
