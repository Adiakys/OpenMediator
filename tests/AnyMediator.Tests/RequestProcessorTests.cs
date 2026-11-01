using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AnyMediator.Tests;

public sealed class RequestProcessorTests
{
    private static ServiceProvider BuildProvider(List<string> trace, Action<AnyMediatorConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddAnyMediator(configure ?? (cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RequestProcessorTests>();
            cfg.AutoRegisterRequestProcessors = true;
        }));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExplicitRegistration_WithoutAutoScan_RunsPreAndPostProcessors()
    {
        var trace = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddAnyMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RequestProcessorTests>();
            cfg.AddRequestPreProcessor<TestPreProcessor1>();
            cfg.AddRequestPostProcessor<TestPostProcessor>();
        });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new TestRequest());

        trace.Should().StartWith(new[] { nameof(TestPreProcessor1) });
        trace.Should().EndWith($"{nameof(TestPostProcessor)}(Test)");
        // TestPreProcessor2 was not explicitly registered and auto-scan is off
        trace.Should().NotContain(nameof(TestPreProcessor2));
    }

    [Fact]
    public async Task PreProcessors_RunBeforeHandler_InRegistrationOrder()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace);
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new TestRequest());

        trace.Should().StartWith(new[] { nameof(TestPreProcessor1), nameof(TestPreProcessor2) });
    }

    [Fact]
    public async Task PostProcessor_RunsAfterHandler_WithHandlerResponse()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace);
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new TestRequest());

        trace.Should().EndWith($"{nameof(TestPostProcessor)}(Test)");
    }

    [Fact]
    public async Task PreProcessor_Throwing_StopsHandlerAndPostProcessor()
    {
        var trace = new List<string>();
        var expected = new InvalidOperationException("pre failed");

        var throwingPre = new Mock<IRequestPreProcessor<TestRequest>>();
        throwingPre.Setup(x => x.Process(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddAnyMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RequestProcessorTests>();
            cfg.AutoRegisterRequestProcessors = true;
        });
        services.AddTransient(_ => throwingPre.Object);

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var act = () => sender.Send(new TestRequest());

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(expected);
        trace.Should().NotContain(s => s.StartsWith(nameof(TestPostProcessor)));
    }

    [Fact]
    public async Task HandlerThrowing_SkipsPostProcessor()
    {
        var trace = new List<string>();
        var expected = new InvalidOperationException("handler failed");

        var throwingHandler = new Mock<IRequestHandler<TestRequest, string>>();
        throwingHandler.Setup(x => x.Handle(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddAnyMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RequestProcessorTests>();
            cfg.AutoRegisterRequestProcessors = true;
        });
        // The scanned TestHandler would be picked first by TryAddTransient. Replace it.
        var existingHandler = services.First(s => s.ServiceType == typeof(IRequestHandler<TestRequest, string>));
        services.Remove(existingHandler);
        services.AddTransient(_ => throwingHandler.Object);

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var act = () => sender.Send(new TestRequest());

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(expected);
        trace.Should().NotContain(s => s.StartsWith(nameof(TestPostProcessor)));
    }

    [Fact]
    public async Task PreProcessors_RunInsideUserPipelineBehavior()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace, cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RequestProcessorTests>();
            cfg.AutoRegisterRequestProcessors = true;
            cfg.AddOpenBehavior(typeof(TracingPipelineBehavior<,>));
        });
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new TestRequest());

        var preIdx = trace.IndexOf(nameof(TestPreProcessor1));
        var behaviorIdx = trace.IndexOf("Behavior.before");
        behaviorIdx.Should().BeLessThan(preIdx, "user behavior wraps pre-processors (outer)");
    }

    [Fact]
    public async Task PostProcessor_RunsInsideUserPipelineBehaviorAfterPortion()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace, cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RequestProcessorTests>();
            cfg.AutoRegisterRequestProcessors = true;
            cfg.AddOpenBehavior(typeof(TracingPipelineBehavior<,>));
        });
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new TestRequest());

        var postIdx = trace.IndexOf($"{nameof(TestPostProcessor)}(Test)");
        var behaviorAfterIdx = trace.IndexOf("Behavior.after");
        postIdx.Should().BeLessThan(behaviorAfterIdx, "user behavior wraps post-processors (outer)");
    }
}

internal sealed class TracingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _trace;
    public TracingPipelineBehavior(List<string> trace) => _trace = trace;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _trace.Add("Behavior.before");
        var response = await next(cancellationToken);
        _trace.Add("Behavior.after");
        return response;
    }
}
