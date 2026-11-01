namespace AnyMediator;

/// <summary>
/// State carried into <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/> so a handler
/// can mark the exception as handled and supply a fallback response.
/// </summary>
public sealed class RequestExceptionHandlerState<TResponse>
{
    /// <summary>
    /// Whether a handler has recovered from the exception.
    /// </summary>
    public bool Handled { get; private set; }

    /// <summary>
    /// The fallback response supplied by <see cref="SetHandled"/>.
    /// </summary>
    public TResponse? Response { get; private set; }

    /// <summary>
    /// Marks the exception as handled: it is not re-thrown and <paramref name="response"/> is returned to the caller.
    /// </summary>
    /// <param name="response">The fallback response.</param>
    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}
