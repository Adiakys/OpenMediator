namespace OpenMediatR;

/// <summary>
/// Handles a request of type <typeparamref name="TRequest"/>. Exactly one handler per request type is expected;
/// handlers are discovered by assembly scanning.
/// </summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a request that returns no value. The <see cref="Unit"/> response is supplied automatically.
/// </summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    new Task Handle(TRequest request, CancellationToken cancellationToken);

    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
    {
        await Handle(request, cancellationToken);
        return Unit.Value;
    }
}
