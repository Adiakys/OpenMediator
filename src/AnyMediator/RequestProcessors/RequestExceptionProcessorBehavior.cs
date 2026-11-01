using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.RequestProcessors;

internal sealed class RequestExceptionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethodCache = new();

    private readonly IServiceProvider _services;

    public RequestExceptionProcessorBehavior(IServiceProvider services)
    {
        _services = services;
    }

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
                var handlerInterface = typeof(IRequestExceptionHandler<,,>)
                    .MakeGenericType(typeof(TRequest), typeof(TResponse), type);

                var handlers = (IEnumerable<object>?)_services.GetService(
                    typeof(IEnumerable<>).MakeGenericType(handlerInterface));

                if (handlers is null) continue;

                var handleMethod = HandleMethodCache.GetOrAdd(handlerInterface, t => t.GetMethod("Handle")!);

                foreach (var handler in handlers)
                {
                    await ((Task)handleMethod.Invoke(handler, [request, exception, state, cancellationToken])!);
                    if (state.Handled) return state.Response!;
                }
            }

            throw;
        }
    }
}
