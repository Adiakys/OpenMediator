using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AnyMediator;

internal sealed class AnyMediatorPublisher : IPublisher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AnyMediatorPublisher> _logger;

    public AnyMediatorPublisher(IServiceProvider services, ILogger<AnyMediatorPublisher> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        var sinks = _services.GetServices<INotificationSink>();

        foreach (var sink in sinks)
        {
            try
            {
                await sink.Dispatch(notification, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification sink {SinkType} failed while dispatching {NotificationType}",
                    sink.GetType().Name, typeof(TNotification).Name);
            }
        }
    }
}