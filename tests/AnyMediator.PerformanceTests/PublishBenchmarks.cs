using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AnyMediator.PerformanceTests;

[MemoryDiagnoser]
public class PublishBenchmarks
{
    private IPublisher _publisherOneHandler = null!;
    private IPublisher _publisherThreeHandlers = null!;
    private BenchmarkNotification _notification = null!;

    [GlobalSetup]
    public void Setup()
    {
        _notification = new BenchmarkNotification();

        _publisherOneHandler = BuildPublisher(typeof(BenchmarkNotificationHandler));
        _publisherThreeHandlers = BuildPublisher(
            typeof(BenchmarkNotificationHandler),
            typeof(BenchmarkNotificationHandler2),
            typeof(BenchmarkNotificationHandler3));

        // Warmup metadata cache
        _publisherOneHandler.Publish(_notification).GetAwaiter().GetResult();
        _publisherThreeHandlers.Publish(_notification).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task Publish_OneHandler()
        => _publisherOneHandler.Publish(_notification);

    [Benchmark]
    public Task Publish_ThreeHandlers()
        => _publisherThreeHandlers.Publish(_notification);

    private static IPublisher BuildPublisher(params Type[] handlerTypes)
    {
        var services = new ServiceCollection();
        services.AddAnyMediator(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<BenchmarkRequest>());

        // Remove auto-scanned handlers and register only the ones we want
        services.RemoveAll(typeof(INotificationHandler<BenchmarkNotification>));
        foreach (var handlerType in handlerTypes)
            services.AddTransient(typeof(INotificationHandler<BenchmarkNotification>), handlerType);

        return services.BuildServiceProvider().GetRequiredService<IPublisher>();
    }
}
