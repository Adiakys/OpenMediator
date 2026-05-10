namespace OpenMediatR;

/// <summary>
/// Runs before a request handler. Multiple pre-processors per request type are supported and execute sequentially in registration order.
/// </summary>
public interface IRequestPreProcessor<in TRequest> where TRequest : notnull
{
    Task Process(TRequest request, CancellationToken cancellationToken);
}
