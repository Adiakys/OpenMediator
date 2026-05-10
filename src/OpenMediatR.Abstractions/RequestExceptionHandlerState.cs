namespace OpenMediatR;

/// <summary>
/// State carried into <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/> so a handler
/// can mark the exception as handled and supply a fallback response.
/// </summary>
public sealed class RequestExceptionHandlerState<TResponse>
{
    public bool Handled { get; private set; }
    public TResponse? Response { get; private set; }

    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}
