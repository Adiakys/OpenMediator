using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace AnyMediator;

// One wrapper per message type, created once and cached. Being generic over the message type, it calls handlers and
// behaviors through their interfaces: no reflection on the dispatch path. The non-generic bases back the object
// overloads of ISender, which only know the message's runtime type.

internal abstract class RequestHandlerWrapper
{
    public abstract Task<object?> HandleBoxed(object request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken);
}

internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapper
{
    public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken);

    public override async Task<object?> HandleBoxed(object request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
        => await Handle((IRequest<TResponse>)request, services, registry, cancellationToken);
}

internal sealed class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
        => Telemetry.IsEnabled
            ? HandleTraced((TRequest)request, services, registry, cancellationToken)
            : HandleCore((TRequest)request, services, registry, cancellationToken);

    private static readonly OperationSpan Span = new("send", typeof(TRequest));

    private static Task<TResponse> HandleTraced(TRequest request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
        => Telemetry.Run(Span, (request, services, registry, cancellationToken),
            static s => HandleCore(s.request, s.services, s.registry, s.cancellationToken));

    private static Task<TResponse> HandleCore(TRequest request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
    {
        var behaviors = services.GetAll<IPipelineBehavior<TRequest, TResponse>>();

        return behaviors.Length == 0
            ? registry.GetRequired<IRequestHandler<TRequest, TResponse>>(services).Handle(request, cancellationToken)
            : RunPipeline(request, behaviors, services, registry, cancellationToken);
    }

    // Apart from Handle so that its closures are only allocated when there are behaviors.
    private static Task<TResponse> RunPipeline(
        TRequest request, IPipelineBehavior<TRequest, TResponse>[] behaviors, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
    {
        // The handler is resolved inside the pipeline, so a behavior can short-circuit before it is needed and
        // exception handlers also see a missing handler.
        RequestHandlerDelegate<TResponse> next = ct => registry.GetRequired<IRequestHandler<TRequest, TResponse>>(services).Handle(request, ct);

        // Wrap from the last registered behavior inwards, so the first registered one is the outermost.
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = ct => behavior.Handle(request, inner, ct);
        }

        return next(cancellationToken);
    }
}

internal abstract class StreamHandlerWrapper
{
    public abstract IAsyncEnumerable<object?> HandleBoxed(object request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken);
}

internal abstract class StreamHandlerWrapper<TResponse> : StreamHandlerWrapper
{
    public abstract IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken);

    // Handle runs now, so a missing handler is reported by the call rather than by the first MoveNextAsync.
    public override IAsyncEnumerable<object?> HandleBoxed(object request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
        => Box(Handle((IStreamRequest<TResponse>)request, services, registry, cancellationToken), cancellationToken);

    private static async IAsyncEnumerable<object?> Box(IAsyncEnumerable<TResponse> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
            yield return item;
    }
}

internal sealed class StreamHandlerWrapper<TRequest, TResponse> : StreamHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public override IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
        => Telemetry.IsEnabled
            ? HandleTraced((TRequest)request, services, registry, cancellationToken)
            : HandleCore((TRequest)request, services, registry, cancellationToken);

    private static readonly OperationSpan Span = new("create_stream", typeof(TRequest));

    private static IAsyncEnumerable<TResponse> HandleTraced(TRequest request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
        => Telemetry.RunStream(Span, (request, services, registry, cancellationToken),
            static s => HandleCore(s.request, s.services, s.registry, s.cancellationToken), cancellationToken);

    private static IAsyncEnumerable<TResponse> HandleCore(TRequest request, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
    {
        var behaviors = services.GetAll<IStreamPipelineBehavior<TRequest, TResponse>>();

        return behaviors.Length == 0
            ? registry.GetRequired<IStreamRequestHandler<TRequest, TResponse>>(services).Handle(request, cancellationToken)
            : RunPipeline(request, behaviors, services, registry, cancellationToken);
    }

    private static IAsyncEnumerable<TResponse> RunPipeline(
        TRequest request, IStreamPipelineBehavior<TRequest, TResponse>[] behaviors, IServiceProvider services, HandlerRegistry registry, CancellationToken cancellationToken)
    {
        StreamHandlerDelegate<TResponse> next = ct => registry.GetRequired<IStreamRequestHandler<TRequest, TResponse>>(services).Handle(request, ct);

        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = ct => behavior.Handle(request, inner, ct);
        }

        return next(cancellationToken);
    }
}

/// <summary>The handlers of one notification type, as found by <see cref="HandlerRegistry.GetNotificationTargets"/>.</summary>
internal abstract class NotificationTarget
{
    public abstract object[] ResolveHandlers(IServiceProvider services);

    public abstract Func<Task> CreateCall(object handler, object notification, CancellationToken cancellationToken);

    public Func<Task>[] CreateCalls(IServiceProvider services, object notification, CancellationToken cancellationToken)
    {
        var handlers = ResolveHandlers(services);
        var calls = new Func<Task>[handlers.Length];
        for (var i = 0; i < handlers.Length; i++)
            calls[i] = CreateCall(handlers[i], notification, cancellationToken);
        return calls;
    }
}

internal sealed class NotificationTarget<TNotification>(HandlerRegistry.ClosedHandler[] genericHandlers) : NotificationTarget
    where TNotification : INotification
{
    // The container's registrations, then the open generic handlers that close to this notification type.
    public override object[] ResolveHandlers(IServiceProvider services)
    {
        object[] registered = services.GetAll<INotificationHandler<TNotification>>();
        return genericHandlers.Length == 0 ? registered : [.. registered, .. genericHandlers.Select(h => h.Factory(services, null))];
    }

    public override Func<Task> CreateCall(object handler, object notification, CancellationToken cancellationToken)
        => Telemetry.IsEnabled ? CreateTracedCall(handler, notification, cancellationToken) : CreatePlainCall(handler, notification, cancellationToken);

    private static Func<Task> CreatePlainCall(object handler, object notification, CancellationToken cancellationToken)
        => () => ((INotificationHandler<TNotification>)handler).Handle((TNotification)notification, cancellationToken);

    private static readonly ConcurrentDictionary<Type, OperationSpan> HandlerSpans = new();

    // One child span per handler, under the publish span.
    private static Func<Task> CreateTracedCall(object handler, object notification, CancellationToken cancellationToken)
    {
        var span = HandlerSpans.GetOrAdd(handler.GetType(), static type => new OperationSpan("handle", typeof(TNotification), type));
        return () => Telemetry.Run(span, (handler, notification, cancellationToken),
            static s => ((INotificationHandler<TNotification>)s.handler).Handle((TNotification)s.notification, s.cancellationToken));
    }
}
