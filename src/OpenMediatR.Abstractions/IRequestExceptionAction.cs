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
    Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
