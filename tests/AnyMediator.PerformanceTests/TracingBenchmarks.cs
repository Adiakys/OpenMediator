using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.PerformanceTests;

// The cost of tracing: the same Send and Publish with nobody listening, and with a listener that records every span
// and every measurement, as an OpenTelemetry SDK sampling 100% does (minus the exporter's own work).
[MemoryDiagnoser]
public class TracingBenchmarks
{
    private ISender _sender = null!;
    private IPublisher _publisher = null!;
    private ActivityListener? _activityListener;
    private MeterListener? _meterListener;
    private readonly BenchmarkRequest _request = new();
    private readonly BenchmarkNotification _notification = new();

    [Params(false, true)]
    public bool Listening { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (Listening)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == AnyMediatorDiagnostics.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(_activityListener);

            _meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AnyMediatorDiagnostics.MeterName)
                        listener.EnableMeasurementEvents(instrument);
                },
            };
            _meterListener.SetMeasurementEventCallback<double>((_, _, _, _) => { });
            _meterListener.Start();
        }

        var provider = new ServiceCollection()
            .AddAnyMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<BenchmarkRequest>())
            .BuildServiceProvider();
        _sender = provider.GetRequiredService<ISender>();
        _publisher = provider.GetRequiredService<IPublisher>();

        Send().GetAwaiter().GetResult();
        Publish().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _activityListener?.Dispose();
        _meterListener?.Dispose();
    }

    [Benchmark]
    public Task<string> Send() => _sender.Send(_request);

    [Benchmark]
    public Task Publish() => _publisher.Publish(_notification);
}
