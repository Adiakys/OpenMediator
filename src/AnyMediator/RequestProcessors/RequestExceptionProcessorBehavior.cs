using System.Reflection;

namespace AnyMediator.RequestProcessors;

internal sealed class RequestExceptionProcessorBehavior<TRequest, TResponse>(IServiceProvider services) : IPipelineBehavior<TRequest, TResponse>
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
            var state = new RequestExceptionHandlerState<TResponse>();

            // Walk the exception type hierarchy from concrete → Exception so a handler registered
            // for a base type (e.g. Exception) still runs when a derived type is thrown.
            for (var type = exception.GetType(); type != null && typeof(Exception).IsAssignableFrom(type); type = type.BaseType)
            {
                var handlerInterface = typeof(IRequestExceptionHandler<,,>).MakeGenericType(typeof(TRequest), typeof(TResponse), type);
                var handle = handlerInterface.GetMethod("Handle")!;

                foreach (var handler in services.GetAll(handlerInterface))
                {
                    await (Task)handle.Invoke(handler, BindingFlags.DoNotWrapExceptions, null, [request, exception, state, cancellationToken], null)!;
                    if (state.Handled) return state.Response!;
                }
            }

            throw;
        }
    }
}
