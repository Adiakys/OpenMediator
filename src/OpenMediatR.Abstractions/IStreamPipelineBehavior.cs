namespace OpenMediatR;

public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>(CancellationToken cancellationToken);

public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
