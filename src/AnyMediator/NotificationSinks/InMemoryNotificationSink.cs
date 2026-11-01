using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.NotificationSinks;

internal sealed class InMemoryNotificationSink : INotificationSink
{
    private static readonly ConcurrentDictionary<Type, NotificationMetadata> MetadataCache = new();

    private readonly IServiceProvider _services;
    private readonly INotificationPublisher _publisher;

    public InMemoryNotificationSink(IServiceProvider services, INotificationPublisher publisher)
    {
        _services = services;
        _publisher = publisher;
    }

    public Task Dispatch<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        var metadata = GetOrCreateMetadata(notification!.GetType());
        var handlers = _services.GetServices(metadata.HandlerType);

        var handlerCalls = handlers.Select(handler => new Func<Task>(async () =>
        {
            try
            {
                await (metadata.HandleMethod.Invoke(handler, [notification, cancellationToken]) as Task)!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }));

        return _publisher.Publish(handlerCalls, cancellationToken);
    }

    private static NotificationMetadata GetOrCreateMetadata(Type notificationType)
    {
        return MetadataCache.GetOrAdd(notificationType, _ =>
        {
            var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
            var handleMethod = handlerType.GetMethod("Handle", [notificationType, typeof(CancellationToken)])!;
            return new NotificationMetadata(handlerType, handleMethod);
        });
    }

    private sealed record NotificationMetadata(Type HandlerType, MethodInfo HandleMethod);
}