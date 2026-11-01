using System.Reflection;

namespace AnyMediator.RequestProcessors;

internal sealed class RequestExceptionActionProcessorBehavior<TRequest, TResponse>(IServiceProvider services) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception exception)
        {
            for (var type = exception.GetType(); type != null && typeof(Exception).IsAssignableFrom(type); type = type.BaseType)
            {
                var actionInterface = typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), type);
                var execute = actionInterface.GetMethod("Execute")!;

                foreach (var action in services.GetAll(actionInterface))
                    await (Task)execute.Invoke(action, BindingFlags.DoNotWrapExceptions, null, [request, exception, cancellationToken], null)!;
            }

            throw;
        }
    }
}
