using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnyMediator.PerformanceTests;

[MemoryDiagnoser]
public class SenderBenchmarks
{
    private ISender _senderNoPipeline = null!;
    private ISender _senderOnePipeline = null!;
    private ISender _senderThreePipelines = null!;
    private BenchmarkRequest _request = null!;

    [GlobalSetup]
    public void Setup()
    {
        _request = new BenchmarkRequest();

        _senderNoPipeline = BuildSender();
        _senderOnePipeline = BuildSender(typeof(BenchmarkPipelineBehavior<,>));
        _senderThreePipelines = BuildSender(
            typeof(BenchmarkPipelineBehavior<,>),
            typeof(BenchmarkPipelineBehavior2<,>),
            typeof(BenchmarkPipelineBehavior3<,>));

        // Warmup metadata cache
        _senderNoPipeline.Send(_request).GetAwaiter().GetResult();
        _senderOnePipeline.Send(_request).GetAwaiter().GetResult();
        _senderThreePipelines.Send(_request).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<string> Send_NoPipeline()
        => _senderNoPipeline.Send(_request);

    [Benchmark]
    public Task<string> Send_WithOnePipeline()
        => _senderOnePipeline.Send(_request);

    [Benchmark]
    public Task<string> Send_WithThreePipelines()
        => _senderThreePipelines.Send(_request);

    private static ISender BuildSender(params Type[] behaviors)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAnyMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<BenchmarkRequest>();
            foreach (var behavior in behaviors)
                cfg.AddOpenBehavior(behavior);
        });
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }
}
