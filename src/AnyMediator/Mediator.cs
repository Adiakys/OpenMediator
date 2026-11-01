namespace AnyMediator;

internal sealed class Mediator(ISender sender, IPublisher publisher) : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => sender.Send(request, cancellationToken);

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => sender.Send(request, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => sender.CreateStream(request, cancellationToken);

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => sender.CreateStream(request, cancellationToken);

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        => publisher.Publish(notification, cancellationToken);

    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => publisher.Publish(notification, cancellationToken);
}
