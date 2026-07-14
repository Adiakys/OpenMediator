using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR;

internal sealed class OpenMediatRSender : ISender
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), RequestMetadata> RequestMetadataCache = new();
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), StreamRequestMetadata> StreamMetadataCache = new();
    private static readonly ConcurrentDictionary<Type, Func<OpenMediatRSender, object, CancellationToken, IAsyncEnumerable<object?>>> StreamObjectDispatchCache = new();

    private readonly IServiceProvider _services;

    public OpenMediatRSender(IServiceProvider services)
    {
        _services = services;
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

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dispatcher = StreamObjectDispatchCache.GetOrAdd(request.GetType(), BuildStreamObjectDispatcher);
        return dispatcher(this, request, cancellationToken);
    }

    private Task<TResponse> Handle<TResponse>(IRequest<TResponse> request, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        try
        {
            var handler = _services.GetRequiredService(metadata.HandlerType);
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
            var handler = _services.GetRequiredService(metadata.HandlerType);
            return (metadata.HandlerMethod.Invoke(handler, [request, cancellationToken]) as IAsyncEnumerable<TResponse>)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }
    }

    private RequestHandlerDelegate<TResponse> ConfigurePipeline<TResponse>(
        IRequest<TResponse> request,
        RequestMetadata metadata,
        RequestHandlerDelegate<TResponse> next)
    {
        var pipelines = _services.GetServicesOrDefault(metadata.PipelineType);

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
        var pipelines = _services.GetServicesOrDefault(metadata.PipelineType);

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

    private static Func<OpenMediatRSender, object, CancellationToken, IAsyncEnumerable<object?>> BuildStreamObjectDispatcher(Type requestType)
    {
        var streamRequestInterface = requestType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>))
            ?? throw new ArgumentException(
                $"{requestType.Name} does not implement {typeof(IStreamRequest<>).Name}.", nameof(requestType));

        var responseType = streamRequestInterface.GetGenericArguments()[0];

        var typedCreateStream = typeof(OpenMediatRSender)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == nameof(CreateStream) && m.IsGenericMethod)
            .MakeGenericMethod(responseType);

        var boxMethod = typeof(OpenMediatRSender)
            .GetMethod(nameof(BoxStream), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(responseType);

        return (sender, req, ct) =>
        {
            var typedStream = typedCreateStream.Invoke(sender, [req, ct])!;
            return (IAsyncEnumerable<object?>)boxMethod.Invoke(null, [typedStream, ct])!;
        };
    }

    private static async IAsyncEnumerable<object?> BoxStream<T>(IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken)
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
