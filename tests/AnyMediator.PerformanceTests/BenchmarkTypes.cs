namespace AnyMediator.PerformanceTests;

public sealed record BenchmarkRequest() : IRequest<string>;

public sealed class BenchmarkRequestHandler : IRequestHandler<BenchmarkRequest, string>
{
    public Task<string> Handle(BenchmarkRequest request, CancellationToken cancellationToken)
        => Task.FromResult("ok");
}

public sealed record BenchmarkNotification() : INotification;

public sealed class BenchmarkNotificationHandler : INotificationHandler<BenchmarkNotification>
{
    public Task Handle(BenchmarkNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class BenchmarkNotificationHandler2 : INotificationHandler<BenchmarkNotification>
{
    public Task Handle(BenchmarkNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class BenchmarkNotificationHandler3 : INotificationHandler<BenchmarkNotification>
{
    public Task Handle(BenchmarkNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class BenchmarkPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => next(cancellationToken);
}

public sealed class BenchmarkPipelineBehavior2<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => next(cancellationToken);
}

public sealed class BenchmarkPipelineBehavior3<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => next(cancellationToken);
}
