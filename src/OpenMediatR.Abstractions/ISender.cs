namespace OpenMediatR;

public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    Task Send(IRequest request, CancellationToken cancellationToken = default)
        => Send<Unit>(request, cancellationToken);

    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
}
