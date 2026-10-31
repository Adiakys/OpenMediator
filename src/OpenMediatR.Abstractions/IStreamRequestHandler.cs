namespace OpenMediatR;

/// <summary>
/// Handles a stream request, producing its items as an <see cref="IAsyncEnumerable{T}"/>. Exactly one handler
/// per stream request type is expected; handlers are discovered by assembly scanning.
/// </summary>
/// <typeparam name="TRequest">The stream request type handled.</typeparam>
/// <typeparam name="TResponse">The type of the streamed items.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the stream request.
    /// </summary>
    /// <param name="request">The stream request.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The stream of responses.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
