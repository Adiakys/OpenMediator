namespace AnyMediator;

/// <summary>
/// Runs before a request handler. Multiple pre-processors per request type are supported and execute sequentially in registration order.
/// </summary>
public interface IRequestPreProcessor<in TRequest> where TRequest : notnull
{
    /// <summary>
    /// Processes the request before the handler runs.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task Process(TRequest request, CancellationToken cancellationToken);
}
