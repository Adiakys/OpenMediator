namespace OpenMediatR;

/// <summary>
/// Side-effect (logging, metrics, ...) triggered by an exception during request processing.
/// Actions cannot recover from the exception; the original exception is always re-thrown after
/// all matching actions complete. Multiple actions per (request, exception) pair are supported and
/// run sequentially in registration order.
/// </summary>
public interface IRequestExceptionAction<in TRequest, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Runs the side-effect for the exception.
    /// </summary>
    /// <param name="request">The request whose processing failed.</param>
    /// <param name="exception">The exception thrown.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
