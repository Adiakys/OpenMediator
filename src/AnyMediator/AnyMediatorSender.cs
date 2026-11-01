using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace AnyMediator;

internal sealed class AnyMediatorSender : ISender
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), RequestMetadata> RequestMetadataCache = new();
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), StreamRequestMetadata> StreamMetadataCache = new();
    private static readonly ConcurrentDictionary<Type, Func<AnyMediatorSender, object, CancellationToken, Task<object?>>> ObjectSendCache = new();
    private static readonly ConcurrentDictionary<Type, Func<AnyMediatorSender, object, CancellationToken, IAsyncEnumerable<object?>>> ObjectStreamCache = new();

    private readonly IServiceProvider _services;
    private readonly HandlerRegistry _registry;

    public AnyMediatorSender(IServiceProvider services, HandlerRegistry registry)
    {
        _services = services;
        _registry = registry;
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var metadata = GetOrCreateRequestMetadata(request.GetType(), typeof(TResponse));
        RequestHandlerDelegate<TResponse> next = (ct) => Handle(request, metadata, ct);
        next = ConfigurePipeline(request, metadata, next);
        return next(cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var metadata = GetOrCreateStreamMetadata(request.GetType(), typeof(TResponse));
        StreamHandlerDelegate<TResponse> next = (ct) => HandleStream(request, metadata, ct);
        next = ConfigureStreamPipeline(request, metadata, next);
        return next(cancellationToken);
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var send = ObjectSendCache.GetOrAdd(request.GetType(), static type =>
            BindToResponseType<Func<AnyMediatorSender, object, CancellationToken, Task<object?>>>(
                nameof(SendBoxed), ResponseType(type, typeof(IRequest<>), requireSingle: true)));
        return send(this, request, cancellationToken);
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var createStream = ObjectStreamCache.GetOrAdd(request.GetType(), static type =>
            BindToResponseType<Func<AnyMediatorSender, object, CancellationToken, IAsyncEnumerable<object?>>>(
                nameof(CreateStreamBoxed), ResponseType(type, typeof(IStreamRequest<>), requireSingle: false)));
        return createStream(this, request, cancellationToken);
    }

    private Task<TResponse> Handle<TResponse>(IRequest<TResponse> request, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        try
        {
            var handler = GetRequiredHandler(metadata.HandlerType, request.GetType());
            return (metadata.HandlerMethod.Invoke(handler, [request, cancellationToken]) as Task<TResponse>)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }
    }

    private IAsyncEnumerable<TResponse> HandleStream<TResponse>(IStreamRequest<TResponse> request, StreamRequestMetadata metadata, CancellationToken cancellationToken)
    {
        try
        {
            var handler = GetRequiredHandler(metadata.HandlerType, request.GetType());
            return (metadata.HandlerMethod.Invoke(handler, [request, cancellationToken]) as IAsyncEnumerable<TResponse>)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }
    }

    private object GetRequiredHandler(Type handlerType, Type requestType)
    {
        return _registry.ResolveSingle(_services, handlerType)
            // Spell out the fix: callers (and coding agents) act on the message without reading the source.
            ?? throw new InvalidOperationException(
                $"No handler registered for request '{requestType.FullName}'. Expected a non-abstract class implementing " +
                $"{handlerType.GetGenericTypeDefinition().Name.Split('`')[0]}<{string.Join(", ", handlerType.GetGenericArguments().Select(t => t.Name))}> " +
                "in an assembly scanned by AddAnyMediator: add cfg.RegisterServicesFromAssemblyContaining<T>() with T declared in the handler's assembly.");
    }

    private RequestHandlerDelegate<TResponse> ConfigurePipeline<TResponse>(
        IRequest<TResponse> request,
        RequestMetadata metadata,
        RequestHandlerDelegate<TResponse> next)
    {
        // Wrap from the last registered behavior inwards, so the first registered one is the outermost
        // and runs first (same order as MediatR).
        var pipelines = _services.GetServicesOrDefault(metadata.PipelineType).Reverse();

        foreach (var pipeline in pipelines)
        {
            var currentNext = next;
            next = (ct) =>
            {
                try
                {
                    var result = metadata.PipelineMethod.Invoke(pipeline, [request, currentNext, ct]);
                    return (Task<TResponse>)result!;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw; // unreachable
                }
            };
        }

        return next;
    }

    private StreamHandlerDelegate<TResponse> ConfigureStreamPipeline<TResponse>(
        IStreamRequest<TResponse> request,
        StreamRequestMetadata metadata,
        StreamHandlerDelegate<TResponse> next)
    {
        var pipelines = _services.GetServicesOrDefault(metadata.PipelineType).Reverse();

        foreach (var pipeline in pipelines)
        {
            var currentNext = next;
            next = (ct) =>
            {
                try
                {
                    var result = metadata.PipelineMethod.Invoke(pipeline, [request, currentNext, ct]);
                    return (IAsyncEnumerable<TResponse>)result!;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw; // unreachable
                }
            };
        }

        return next;
    }

    private static RequestMetadata GetOrCreateRequestMetadata(Type requestType, Type responseType)
    {
        return RequestMetadataCache.GetOrAdd((requestType, responseType), static key =>
        {
            var (requestType, responseType) = key;

            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
            var handlerMethod = handlerType.GetMethod("Handle", [requestType, typeof(CancellationToken)])!;

            var pipelineType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
            var pipelineMethod = pipelineType.GetMethod("Handle");

            return new RequestMetadata(handlerType, handlerMethod, pipelineType, pipelineMethod!);
        });
    }

    private static StreamRequestMetadata GetOrCreateStreamMetadata(Type requestType, Type responseType)
    {
        return StreamMetadataCache.GetOrAdd((requestType, responseType), static key =>
        {
            var (requestType, responseType) = key;

            var handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);
            var handlerMethod = handlerType.GetMethod("Handle", [requestType, typeof(CancellationToken)])!;

            var pipelineType = typeof(IStreamPipelineBehavior<,>).MakeGenericType(requestType, responseType);
            var pipelineMethod = pipelineType.GetMethod("Handle");

            return new StreamRequestMetadata(handlerType, handlerMethod, pipelineType, pipelineMethod!);
        });
    }

    /// <summary>
    /// The <c>TResponse</c> of the closed <paramref name="openInterface"/> a request implements. With several, a request is
    /// ambiguous and throws; a stream request keeps the first, as it always has.
    /// </summary>
    private static Type ResponseType(Type requestType, Type openInterface, bool requireSingle)
    {
        var responseTypes = requestType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface)
            .Select(i => i.GetGenericArguments()[0])
            .ToArray();

        var interfaceName = openInterface.Name.Split('`')[0];

        if (responseTypes.Length == 0)
            throw new ArgumentException($"{requestType.FullName} does not implement {interfaceName}<TResponse>.", "request");

        if (requireSingle && responseTypes.Length > 1)
            throw new ArgumentException(
                $"{requestType.FullName} implements {interfaceName}<TResponse> for more than one response type " +
                $"({string.Join(", ", responseTypes.Select(t => t.Name))}): call the generic overload with the one you want.", "request");

        return responseTypes[0];
    }

    private static TDelegate BindToResponseType<TDelegate>(string methodName, Type responseType) where TDelegate : Delegate
        => typeof(AnyMediatorSender)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(responseType)
            .CreateDelegate<TDelegate>();

    private static async Task<object?> SendBoxed<TResponse>(AnyMediatorSender sender, object request, CancellationToken cancellationToken)
        => await sender.Send((IRequest<TResponse>)request, cancellationToken);

    private static IAsyncEnumerable<object?> CreateStreamBoxed<TResponse>(AnyMediatorSender sender, object request, CancellationToken cancellationToken)
        => Box(sender.CreateStream((IStreamRequest<TResponse>)request, cancellationToken), cancellationToken);

    private static async IAsyncEnumerable<object?> Box<T>(IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
            yield return item;
    }

    private sealed record RequestMetadata(
        Type HandlerType,
        MethodInfo HandlerMethod,
        Type PipelineType,
        MethodInfo PipelineMethod);

    private sealed record StreamRequestMetadata(
        Type HandlerType,
        MethodInfo HandlerMethod,
        Type PipelineType,
        MethodInfo PipelineMethod);
}
