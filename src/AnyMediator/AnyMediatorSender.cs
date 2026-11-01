using System.Collections.Concurrent;

namespace AnyMediator;

internal sealed class AnyMediatorSender(IServiceProvider services, HandlerRegistry registry) : ISender
{
    private static readonly ConcurrentDictionary<(Type Request, Type Response), RequestHandlerWrapper> RequestWrappers = new();
    private static readonly ConcurrentDictionary<(Type Request, Type Response), StreamHandlerWrapper> StreamWrappers = new();
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapper> ObjectRequestWrappers = new();
    private static readonly ConcurrentDictionary<Type, StreamHandlerWrapper> ObjectStreamWrappers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = RequestWrappers.GetOrAdd((request.GetType(), typeof(TResponse)),
            static key => Create<RequestHandlerWrapper>(typeof(RequestHandlerWrapper<,>), key.Request, key.Response));
        return ((RequestHandlerWrapper<TResponse>)wrapper).Handle(request, services, registry, cancellationToken);
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = ObjectRequestWrappers.GetOrAdd(request.GetType(), static type =>
            Create<RequestHandlerWrapper>(typeof(RequestHandlerWrapper<,>), type, ResponseType(type, typeof(IRequest<>), requireSingle: true)));
        return wrapper.HandleBoxed(request, services, registry, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = StreamWrappers.GetOrAdd((request.GetType(), typeof(TResponse)),
            static key => Create<StreamHandlerWrapper>(typeof(StreamHandlerWrapper<,>), key.Request, key.Response));
        return ((StreamHandlerWrapper<TResponse>)wrapper).Handle(request, services, registry, cancellationToken);
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = ObjectStreamWrappers.GetOrAdd(request.GetType(), static type =>
            Create<StreamHandlerWrapper>(typeof(StreamHandlerWrapper<,>), type, ResponseType(type, typeof(IStreamRequest<>), requireSingle: false)));
        return wrapper.HandleBoxed(request, services, registry, cancellationToken);
    }

    private static TWrapper Create<TWrapper>(Type openWrapperType, Type requestType, Type responseType)
        => (TWrapper)Activator.CreateInstance(openWrapperType.MakeGenericType(requestType, responseType))!;

    /// <summary>
    /// The <c>TResponse</c> of the closed <paramref name="openInterface"/> a request implements. With several, a request is
    /// ambiguous and throws; a stream request keeps the first, as it always has.
    /// </summary>
    private static Type ResponseType(Type requestType, Type openInterface, bool requireSingle)
    {
        var responseTypes = requestType.GetClosedInterfaces(openInterface).Select(i => i.GetGenericArguments()[0]).ToArray();
        var interfaceName = openInterface.Name.Split('`')[0];

        if (responseTypes.Length == 0)
            throw new ArgumentException($"{requestType.FullName} does not implement {interfaceName}<TResponse>.", "request");

        if (requireSingle && responseTypes.Length > 1)
            throw new ArgumentException(
                $"{requestType.FullName} implements {interfaceName}<TResponse> for more than one response type " +
                $"({string.Join(", ", responseTypes.Select(t => t.Name))}): call the generic overload with the one you want.", "request");

        return responseTypes[0];
    }
}
