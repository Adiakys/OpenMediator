namespace AnyMediator;

/// <summary>
/// Runs after a successful request handler. Multiple post-processors per request type are supported and execute sequentially in registration order.
/// Skipped when the handler or any preceding pipeline step throws.
/// </summary>
public interface IRequestPostProcessor<in TRequest, in TResponse> where TRequest : notnull
{
    /// <summary>
    /// Processes the request after the handler returned.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="response">The response returned by the handler.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
