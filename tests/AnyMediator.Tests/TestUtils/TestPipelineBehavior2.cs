namespace AnyMediator.Tests;

internal sealed class TestPipelineBehavior2<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public int Count { get; private set; } = 0;

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Count++;
        return next(cancellationToken);
    }
}