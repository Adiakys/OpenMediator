namespace AnyMediator.RequestProcessors;

internal sealed class RequestPreProcessorBehavior<TRequest, TResponse>(IEnumerable<IRequestPreProcessor<TRequest>> preProcessors)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        foreach (var preProcessor in preProcessors)
            await preProcessor.Process(request, cancellationToken);

        return await next(cancellationToken);
    }
}
