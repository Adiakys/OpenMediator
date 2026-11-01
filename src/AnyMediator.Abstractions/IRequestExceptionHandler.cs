namespace AnyMediator;

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
    /// <summary>
    /// Handles the exception. Call <see cref="RequestExceptionHandlerState{TResponse}.SetHandled"/> on
    /// <paramref name="state"/> to recover with a fallback response; otherwise the exception keeps propagating.
    /// </summary>
    /// <param name="request">The request whose processing failed.</param>
    /// <param name="exception">The exception thrown.</param>
    /// <param name="state">State used to mark the exception as handled.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task Handle(
        TRequest request,
        TException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken);
}
