using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace OpenMediatR.Tests;

public sealed class RequestExceptionTests
{
    private static ServiceProvider BuildProvider(
        List<string>? trace = null,
        TestExceptionRecoveryResponse? recovery = null,
        IRequestHandler<TestRequest, string>? handlerOverride = null,
        Action<OpenMediatRConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        if (trace is not null) services.AddSingleton(trace);
        if (recovery is not null) services.AddSingleton(recovery);

        services.AddOpenMediatR(configure ?? (cfg =>
            cfg.RegisterServicesFromAssemblyContaining<RequestExceptionTests>()));

        if (handlerOverride is not null)
        {
            var existing = services.First(s => s.ServiceType == typeof(IRequestHandler<TestRequest, string>));
            services.Remove(existing);
            services.AddTransient(_ => handlerOverride);
        }

        return services.BuildServiceProvider();
    }

    private static IRequestHandler<TestRequest, string> ThrowingHandler(Exception exception)
    {
        var mock = new Mock<IRequestHandler<TestRequest, string>>();
        mock.Setup(x => x.Handle(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(exception);
        return mock.Object;
    }

    [Fact]
    public async Task ExceptionHandler_RecoversWithFallbackResponse_WhenSetHandledIsCalled()
    {
        var trace = new List<string>();
        var recovery = new TestExceptionRecoveryResponse("recovered");

        using var provider = BuildProvider(
            trace: trace,
            recovery: recovery,
            handlerOverride: ThrowingHandler(new InvalidOperationException("boom")));

        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new TestRequest());

        result.Should().Be("recovered");
        trace.Should().Contain($"{nameof(TestExceptionHandler)}:boom");
    }

    [Fact]
    public async Task ExceptionHandler_NotMatching_LetsExceptionPropagate()
    {
        var trace = new List<string>();
        // No recovery registered → handler runs but does not call SetHandled → exception bubbles up
        using var provider = BuildProvider(
            trace: trace,
            handlerOverride: ThrowingHandler(new InvalidOperationException("boom")));

        var sender = provider.GetRequiredService<ISender>();

        var act = () => sender.Send(new TestRequest());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task ExceptionHandler_MatchesBaseExceptionType()
    {
        // TestExceptionHandler is registered for InvalidOperationException.
        // Throwing a derived type (ObjectDisposedException : InvalidOperationException) should still match.
        var trace = new List<string>();
        var recovery = new TestExceptionRecoveryResponse("recovered");

        using var provider = BuildProvider(
            trace: trace,
            recovery: recovery,
            handlerOverride: ThrowingHandler(new ObjectDisposedException("X")));

        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new TestRequest());

        result.Should().Be("recovered");
    }

    [Fact]
    public async Task ExceptionAction_RunsOnUnhandledException_AndOriginalExceptionIsRethrown()
    {
        // No recovery registered → action runs, exception rethrown unchanged
        var trace = new List<string>();
        var thrown = new InvalidOperationException("boom");

        using var provider = BuildProvider(
            trace: trace,
            handlerOverride: ThrowingHandler(thrown));

        var sender = provider.GetRequiredService<ISender>();

        var act = () => sender.Send(new TestRequest());

        var caught = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        caught.Should().BeSameAs(thrown);
        trace.Should().Contain($"{nameof(TestExceptionAction)}:InvalidOperationException");
    }

    [Fact]
    public async Task ExceptionAction_DoesNotRun_WhenHandlerRecoveredException()
    {
        // Handler recovers via SetHandled → no exception bubbles up to the action wrapper
        var trace = new List<string>();
        var recovery = new TestExceptionRecoveryResponse("recovered");

        using var provider = BuildProvider(
            trace: trace,
            recovery: recovery,
            handlerOverride: ThrowingHandler(new InvalidOperationException("boom")));

        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new TestRequest());

        trace.Should().NotContain(s => s.StartsWith(nameof(TestExceptionAction)));
    }

    [Fact]
    public async Task ExceptionAction_MatchesBaseExceptionType()
    {
        // TestExceptionAction is registered for Exception (ultimate base class) so it should
        // match every thrown exception type.
        var trace = new List<string>();

        using var provider = BuildProvider(
            trace: trace,
            handlerOverride: ThrowingHandler(new ArgumentNullException("p")));

        var sender = provider.GetRequiredService<ISender>();

        var act = () => sender.Send(new TestRequest());

        await act.Should().ThrowAsync<ArgumentNullException>();
        trace.Should().Contain($"{nameof(TestExceptionAction)}:ArgumentNullException");
    }

    [Fact]
    public async Task ExceptionAction_WithApplyForAllExceptionsStrategy_RunsEvenWhenHandlerRecovers()
    {
        // ApplyForAllExceptions reverses the wrapper order so the action sees the exception first,
        // before the handler gets a chance to recover.
        var trace = new List<string>();
        var recovery = new TestExceptionRecoveryResponse("recovered");

        using var provider = BuildProvider(
            trace: trace,
            recovery: recovery,
            handlerOverride: ThrowingHandler(new InvalidOperationException("boom")),
            configure: cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining<RequestExceptionTests>();
                cfg.RequestExceptionActionProcessorStrategy =
                    RequestExceptionActionProcessorStrategy.ApplyForAllExceptions;
            });

        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new TestRequest());

        result.Should().Be("recovered");
        trace.Should().Contain($"{nameof(TestExceptionAction)}:InvalidOperationException");
        trace.Should().Contain($"{nameof(TestExceptionHandler)}:boom");
    }

    [Fact]
    public async Task ExceptionAction_WithDefaultStrategy_DoesNotRunWhenHandlerRecovers()
    {
        // Sanity check: the default ApplyForUnhandledExceptions strategy preserves earlier behavior.
        var trace = new List<string>();
        var recovery = new TestExceptionRecoveryResponse("recovered");

        using var provider = BuildProvider(
            trace: trace,
            recovery: recovery,
            handlerOverride: ThrowingHandler(new InvalidOperationException("boom")));

        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new TestRequest());

        trace.Should().Contain($"{nameof(TestExceptionHandler)}:boom");
        trace.Should().NotContain(s => s.StartsWith(nameof(TestExceptionAction)));
    }

    [Fact]
    public async Task ExplicitRegistration_WorksWithoutAssemblyScan()
    {
        var trace = new List<string>();
        var recovery = new TestExceptionRecoveryResponse("recovered");

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(trace);
        services.AddSingleton(recovery);
        services.AddTransient(_ => ThrowingHandler(new InvalidOperationException("boom")));
        services.AddOpenMediatR(cfg =>
        {
            cfg.AddRequestExceptionHandler<TestExceptionHandler>();
            cfg.AddRequestExceptionAction<TestExceptionAction>();
        });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new TestRequest());
        result.Should().Be("recovered");
        trace.Should().Contain($"{nameof(TestExceptionHandler)}:boom");
        // Recovered → action does not run
        trace.Should().NotContain(s => s.StartsWith(nameof(TestExceptionAction)));
    }
}
