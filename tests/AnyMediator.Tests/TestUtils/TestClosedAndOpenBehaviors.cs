using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.Tests;

/// <summary>Closed-generic pipeline behavior bound to a specific (request, response) pair.</summary>
internal sealed class TestClosedPipelineBehavior : IPipelineBehavior<TestRequest, string>
{
    public Task<string> Handle(TestRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        => next(cancellationToken);
}

/// <summary>Closed-generic stream pipeline behavior.</summary>
internal sealed class TestClosedStreamPipelineBehavior : IStreamPipelineBehavior<TestStreamRequest, int>
{
    public IAsyncEnumerable<int> Handle(TestStreamRequest request, StreamHandlerDelegate<int> next, CancellationToken cancellationToken)
        => next(cancellationToken);
}

/// <summary>Open-generic pre-processor that traces every request type it sees.</summary>
internal sealed class TestOpenPreProcessor<TRequest> : IRequestPreProcessor<TRequest> where TRequest : notnull
{
    private readonly IServiceProvider _services;
    public TestOpenPreProcessor(IServiceProvider services) => _services = services;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _services.GetService<List<string>>()?.Add($"OpenPre:{typeof(TRequest).Name}");
        return Task.CompletedTask;
    }
}

/// <summary>Open-generic post-processor that traces every (request, response) pair.</summary>
internal sealed class TestOpenPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _services;
    public TestOpenPostProcessor(IServiceProvider services) => _services = services;

    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        _services.GetService<List<string>>()?.Add($"OpenPost:{typeof(TRequest).Name}");
        return Task.CompletedTask;
    }
}
