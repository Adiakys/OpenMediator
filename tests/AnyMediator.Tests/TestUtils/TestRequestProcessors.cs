using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.Tests;

internal sealed class TestPreProcessor1 : IRequestPreProcessor<TestRequest>
{
    private readonly IServiceProvider _services;
    public TestPreProcessor1(IServiceProvider services) => _services = services;

    public Task Process(TestRequest request, CancellationToken cancellationToken)
    {
        _services.GetService<List<string>>()?.Add(nameof(TestPreProcessor1));
        return Task.CompletedTask;
    }
}

internal sealed class TestPreProcessor2 : IRequestPreProcessor<TestRequest>
{
    private readonly IServiceProvider _services;
    public TestPreProcessor2(IServiceProvider services) => _services = services;

    public Task Process(TestRequest request, CancellationToken cancellationToken)
    {
        _services.GetService<List<string>>()?.Add(nameof(TestPreProcessor2));
        return Task.CompletedTask;
    }
}

internal sealed class TestPostProcessor : IRequestPostProcessor<TestRequest, string>
{
    private readonly IServiceProvider _services;
    public TestPostProcessor(IServiceProvider services) => _services = services;

    public Task Process(TestRequest request, string response, CancellationToken cancellationToken)
    {
        _services.GetService<List<string>>()?.Add($"{nameof(TestPostProcessor)}({response})");
        return Task.CompletedTask;
    }
}
