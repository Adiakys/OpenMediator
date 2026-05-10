namespace OpenMediatR;

/// <summary>
/// Runs after a successful request handler. Multiple post-processors per request type are supported and execute sequentially in registration order.
/// Skipped when the handler or any preceding pipeline step throws.
/// </summary>
public interface IRequestPostProcessor<in TRequest, in TResponse> where TRequest : notnull
{
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
