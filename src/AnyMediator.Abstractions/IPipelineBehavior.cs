namespace AnyMediator;

/// <summary>
/// Invokes the next step of the request pipeline: the next behavior, or the handler itself when last.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="cancellationToken">Token to observe for cancellation.</param>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

/// <summary>
/// Middleware wrapped around request handlers, for cross-cutting concerns such as logging, validation,
/// transactions or caching. Behaviors are not discovered by assembly scanning: register them explicitly,
/// for example with <c>cfg.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;))</c>. They run in registration order:
/// the first registered behavior is the outermost.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Runs the behavior. Call <paramref name="next"/> to continue the pipeline, or return without calling it
    /// to short-circuit.
    /// </summary>
    /// <param name="request">The request being sent.</param>
    /// <param name="next">The next step of the pipeline.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The response returned to the caller.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
