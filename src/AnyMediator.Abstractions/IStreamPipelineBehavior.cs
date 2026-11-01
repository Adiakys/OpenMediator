namespace AnyMediator;

/// <summary>
/// Invokes the next step of the stream pipeline: the next behavior, or the stream handler itself when last.
/// </summary>
/// <typeparam name="TResponse">The type of the streamed items.</typeparam>
/// <param name="cancellationToken">Token to observe for cancellation.</param>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>(CancellationToken cancellationToken = default);

/// <summary>
/// Middleware wrapped around stream request handlers. Register it explicitly, for example with
/// <c>cfg.AddOpenStreamBehavior(typeof(StreamLoggingBehavior&lt;,&gt;))</c>. They run in registration order:
/// the first registered behavior is the outermost.
/// </summary>
/// <typeparam name="TRequest">The stream request type.</typeparam>
/// <typeparam name="TResponse">The type of the streamed items.</typeparam>
/// <remarks>
/// The constraint is <c>notnull</c>, as in MediatR, so stream behaviors written for MediatR compile unchanged.
/// </remarks>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Runs the behavior. Call <paramref name="next"/> to obtain the inner stream, then yield, filter or
    /// transform its items.
    /// </summary>
    /// <param name="request">The stream request.</param>
    /// <param name="next">The next step of the pipeline.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The stream returned to the caller.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
