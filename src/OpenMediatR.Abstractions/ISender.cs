namespace OpenMediatR;

/// <summary>
/// Entry point for sending requests to their single handler, through the configured pipeline behaviors.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends a request to its <see cref="IRequestHandler{TRequest, TResponse}"/> through the pipeline.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The response produced by the handler.</returns>
    /// <exception cref="InvalidOperationException">No handler is registered for the request type.</exception>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request that returns no value to its <see cref="IRequestHandler{TRequest}"/> through the pipeline.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <exception cref="InvalidOperationException">No handler is registered for the request type.</exception>
    Task Send(IRequest request, CancellationToken cancellationToken = default)
        => Send<Unit>(request, cancellationToken);

    /// <summary>
    /// Creates a stream of responses from the <see cref="IStreamRequestHandler{TRequest, TResponse}"/> of the
    /// request, through the configured stream pipeline behaviors.
    /// </summary>
    /// <typeparam name="TResponse">The type of the streamed items.</typeparam>
    /// <param name="request">The stream request.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The stream of responses.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream of responses for a request whose type is only known at runtime. The request must
    /// implement <see cref="IStreamRequest{TResponse}"/>; items are returned boxed.
    /// </summary>
    /// <param name="request">The stream request.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The stream of responses.</returns>
    /// <exception cref="ArgumentException"><paramref name="request"/> does not implement <see cref="IStreamRequest{TResponse}"/>.</exception>
    IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
}
