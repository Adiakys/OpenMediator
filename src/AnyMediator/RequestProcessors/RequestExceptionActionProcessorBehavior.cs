using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.RequestProcessors;

internal sealed class RequestExceptionActionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> ExecuteMethodCache = new();

    private readonly IServiceProvider _services;

    public RequestExceptionActionProcessorBehavior(IServiceProvider services)
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
            for (var type = exception.GetType(); type != null && typeof(Exception).IsAssignableFrom(type); type = type.BaseType)
            {
                var actionInterface = typeof(IRequestExceptionAction<,>)
                    .MakeGenericType(typeof(TRequest), type);

                var actions = (IEnumerable<object>?)_services.GetService(
                    typeof(IEnumerable<>).MakeGenericType(actionInterface));

                if (actions is null) continue;

                var executeMethod = ExecuteMethodCache.GetOrAdd(actionInterface, t => t.GetMethod("Execute")!);

                foreach (var action in actions)
                    await ((Task)executeMethod.Invoke(action, [request, exception, cancellationToken])!);
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw; // unreachable
        }
    }
}
