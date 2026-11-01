using System.Reflection;
using System.Runtime.ExceptionServices;

namespace AnyMediator.NotificationSinks;

internal sealed class InMemoryNotificationSink : INotificationSink
{
    private readonly IServiceProvider _services;
    private readonly HandlerRegistry _registry;
    private readonly INotificationPublisher _publisher;

    public InMemoryNotificationSink(IServiceProvider services, HandlerRegistry registry, INotificationPublisher publisher)
    {
        _services = services;
        _registry = registry;
        _publisher = publisher;
    }

    public Task Dispatch<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        var targets = _registry.GetNotificationTargets(notification!.GetType());
        var handlerCalls = new List<Func<Task>>();

        // With handlers for base types too, a class that already ran for a more specific type is skipped, so a class
        // handling both OrderPlaced and IDomainEvent runs once; several instances registered for one type all run.
        var classesDone = targets.Length > 1 ? new HashSet<Type>() : null;

        foreach (var target in targets)
        {
            var classesOfTarget = classesDone is null ? null : new HashSet<Type>();

            foreach (var handler in _registry.ResolveAll(_services, target))
            {
                if (classesDone is not null)
                {
                    var handlerClass = handler.GetType();
                    if (handlerClass.IsGenericType)
                        handlerClass = handlerClass.GetGenericTypeDefinition();

                    if (classesDone.Contains(handlerClass))
                        continue;

                    classesOfTarget!.Add(handlerClass);
                }

                handlerCalls.Add(() => Invoke(target.HandleMethod, handler, notification, cancellationToken));
            }

            if (classesOfTarget is not null)
                classesDone!.UnionWith(classesOfTarget);
        }

        return _publisher.Publish(handlerCalls, cancellationToken);
    }

    private static async Task Invoke(MethodInfo handleMethod, object handler, object notification, CancellationToken cancellationToken)
    {
        try
        {
            await (handleMethod.Invoke(handler, [notification, cancellationToken]) as Task)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
