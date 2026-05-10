namespace OpenMediatR;

/// <summary>
/// Handles exceptions thrown during request processing. Implementations may recover by calling
/// <see cref="RequestExceptionHandlerState{TResponse}.SetHandled"/> with a fallback response.
/// Multiple handlers per (request, exception) pair are supported and run sequentially in registration
/// order; iteration stops once a handler marks the state as handled.
/// </summary>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : notnull
    where TException : Exception
{
    Task Handle(
        TRequest request,
        TException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken);
}
