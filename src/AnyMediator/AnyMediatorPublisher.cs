using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator;

internal sealed class AnyMediatorPublisher : IPublisher
{
    private static readonly ConcurrentDictionary<Type, Func<AnyMediatorPublisher, object, CancellationToken, Task>> ObjectPublishCache = new();

    private readonly IServiceProvider _services;

    public AnyMediatorPublisher(IServiceProvider services)
    {
        _services = services;
    }

    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Every sink runs even if an earlier one fails; the failures reach the caller afterwards, as in MediatR.
        List<Exception>? failures = null;

        foreach (var sink in _services.GetServices<INotificationSink>())
        {
            try
            {
                await sink.Dispatch(notification, cancellationToken);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is null)
            return;

        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();

        throw new AggregateException(failures);
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Closed over the runtime type, so every sink receives the concrete notification type as TNotification.
        var publish = ObjectPublishCache.GetOrAdd(notification.GetType(), static type => typeof(INotification).IsAssignableFrom(type)
            ? typeof(AnyMediatorPublisher)
                .GetMethod(nameof(PublishTyped), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(type)
                .CreateDelegate<Func<AnyMediatorPublisher, object, CancellationToken, Task>>()
            : throw new ArgumentException($"{type.FullName} does not implement {nameof(INotification)}.", "notification"));
        return publish(this, notification, cancellationToken);
    }

    private static Task PublishTyped<TNotification>(AnyMediatorPublisher publisher, object notification, CancellationToken cancellationToken)
        where TNotification : INotification
        => publisher.Publish((TNotification)notification, cancellationToken);
}
