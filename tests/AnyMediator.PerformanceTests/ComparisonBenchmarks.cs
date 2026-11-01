using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;

namespace Comparison;

// The same request and notification sent through AnyMediator, MediatR 12.5.0 (the last Apache-2.0 release) and
// Mediator 3.0.2 (source generated), each with its default registration. One handler, no behaviors.
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ComparisonBenchmarks
{
    private AnyMediator.ISender _anySender = null!;
    private AnyMediator.IPublisher _anyPublisher = null!;
    private MediatR.ISender _mediatRSender = null!;
    private MediatR.IPublisher _mediatRPublisher = null!;
    private Mediator.ISender _mediatorSender = null!;
    private Mediator.IPublisher _mediatorPublisher = null!;

    private readonly AnyPing _anyPing = new();
    private readonly AnyPinged _anyPinged = new();
    private readonly MediatRPing _mediatRPing = new();
    private readonly MediatRPinged _mediatRPinged = new();
    private readonly MediatorPing _mediatorPing = new();
    private readonly MediatorPinged _mediatorPinged = new();

    [GlobalSetup]
    public void Setup()
    {
        // Called through the class: a using for AnyMediator would make IRequest and friends ambiguous here.
        var any = AnyMediator.ServiceCollectionExtensions
            .AddAnyMediator(new ServiceCollection(), cfg => cfg.RegisterServicesFromAssemblyContaining<AnyPing>())
            .BuildServiceProvider();
        _anySender = any.GetRequiredService<AnyMediator.ISender>();
        _anyPublisher = any.GetRequiredService<AnyMediator.IPublisher>();

        var mediatR = new ServiceCollection()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRPing>())
            .BuildServiceProvider();
        _mediatRSender = mediatR.GetRequiredService<MediatR.ISender>();
        _mediatRPublisher = mediatR.GetRequiredService<MediatR.IPublisher>();

        var mediator = new ServiceCollection();
        mediator.AddMediator();
        var mediatorProvider = mediator.BuildServiceProvider();
        _mediatorSender = mediatorProvider.GetRequiredService<Mediator.ISender>();
        _mediatorPublisher = mediatorProvider.GetRequiredService<Mediator.IPublisher>();

        // Fill every library's caches before measuring.
        Send_AnyMediator().GetAwaiter().GetResult();
        Send_MediatR().GetAwaiter().GetResult();
        Send_Mediator().GetAwaiter().GetResult();
        Publish_AnyMediator().GetAwaiter().GetResult();
        Publish_MediatR().GetAwaiter().GetResult();
        Publish_Mediator().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Send")]
    public Task<string> Send_MediatR() => _mediatRSender.Send(_mediatRPing);

    [Benchmark, BenchmarkCategory("Send")]
    public Task<string> Send_AnyMediator() => _anySender.Send(_anyPing);

    [Benchmark, BenchmarkCategory("Send")]
    public ValueTask<string> Send_Mediator() => _mediatorSender.Send(_mediatorPing);

    [Benchmark(Baseline = true), BenchmarkCategory("Publish")]
    public Task Publish_MediatR() => _mediatRPublisher.Publish(_mediatRPinged);

    [Benchmark, BenchmarkCategory("Publish")]
    public Task Publish_AnyMediator() => _anyPublisher.Publish(_anyPinged);

    [Benchmark, BenchmarkCategory("Publish")]
    public ValueTask Publish_Mediator() => _mediatorPublisher.Publish(_mediatorPinged);
}

public sealed record AnyPing : AnyMediator.IRequest<string>;

public sealed class AnyPingHandler : AnyMediator.IRequestHandler<AnyPing, string>
{
    public Task<string> Handle(AnyPing request, CancellationToken cancellationToken) => Task.FromResult("pong");
}

public sealed record AnyPinged : AnyMediator.INotification;

public sealed class AnyPingedHandler : AnyMediator.INotificationHandler<AnyPinged>
{
    public Task Handle(AnyPinged notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed record MediatRPing : MediatR.IRequest<string>;

public sealed class MediatRPingHandler : MediatR.IRequestHandler<MediatRPing, string>
{
    public Task<string> Handle(MediatRPing request, CancellationToken cancellationToken) => Task.FromResult("pong");
}

public sealed record MediatRPinged : MediatR.INotification;

public sealed class MediatRPingedHandler : MediatR.INotificationHandler<MediatRPinged>
{
    public Task Handle(MediatRPinged notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed record MediatorPing : Mediator.IRequest<string>;

public sealed class MediatorPingHandler : Mediator.IRequestHandler<MediatorPing, string>
{
    public ValueTask<string> Handle(MediatorPing request, CancellationToken cancellationToken) => ValueTask.FromResult("pong");
}

public sealed record MediatorPinged : Mediator.INotification;

public sealed class MediatorPingedHandler : Mediator.INotificationHandler<MediatorPinged>
{
    public ValueTask Handle(MediatorPinged notification, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
