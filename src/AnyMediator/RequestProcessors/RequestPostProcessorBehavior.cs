namespace AnyMediator.RequestProcessors;

internal sealed class RequestPostProcessorBehavior<TRequest, TResponse>(IEnumerable<IRequestPostProcessor<TRequest, TResponse>> postProcessors)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        foreach (var postProcessor in postProcessors)
            await postProcessor.Process(request, response, cancellationToken);

        return response;
    }
}
