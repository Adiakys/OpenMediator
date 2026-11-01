using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.Tests;

public sealed class PipelineOrderTests
{
    private static ServiceProvider BuildProvider(
        List<string> trace,
        Action<AnyMediatorConfiguration> configure,
        TestExceptionRecoveryResponse? recovery = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(trace);
        if (recovery is not null) services.AddSingleton(recovery);
        services.AddAnyMediator(configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_RunsBehaviorsInRegistrationOrder_FirstRegisteredIsOutermost()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace, cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<PipelineOrderTests>();
            cfg.AddOpenBehavior(typeof(OuterTracingBehavior<,>));
            cfg.AddOpenBehavior(typeof(InnerTracingBehavior<,>));
        });

        await provider.GetRequiredService<ISender>().Send(new TestRequest());

        trace.Should().Equal("Outer.before", "Inner.before", "Inner.after", "Outer.after");
    }

    [Fact]
    public async Task Send_ProcessorsRunInsideAllUserBehaviors()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace, cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<PipelineOrderTests>();
            cfg.AddOpenBehavior(typeof(OuterTracingBehavior<,>));
            cfg.AddOpenBehavior(typeof(InnerTracingBehavior<,>));
            cfg.AddRequestPreProcessor<TestPreProcessor1>();
            cfg.AddRequestPostProcessor<TestPostProcessor>();
        });

        await provider.GetRequiredService<ISender>().Send(new TestRequest());

        trace.Should().Equal(
            "Outer.before", "Inner.before",
            nameof(TestPreProcessor1), $"{nameof(TestPostProcessor)}(Test)",
            "Inner.after", "Outer.after");
    }

    [Fact]
    public async Task Send_ExceptionHandlerCatchesExceptionThrownByUserBehavior()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace, cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<PipelineOrderTests>();
            cfg.AddOpenBehavior(typeof(ThrowingBehavior<,>));
        }, new TestExceptionRecoveryResponse("recovered"));

        var result = await provider.GetRequiredService<ISender>().Send(new TestRequest());

        result.Should().Be("recovered");
        trace.Should().Contain($"{nameof(TestExceptionHandler)}:behavior failed");
    }

    [Fact]
    public async Task CreateStream_RunsStreamBehaviorsInRegistrationOrder_FirstRegisteredIsOutermost()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace, cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<PipelineOrderTests>();
            cfg.AddOpenStreamBehavior(typeof(OuterTracingStreamBehavior<,>));
            cfg.AddOpenStreamBehavior(typeof(InnerTracingStreamBehavior<,>));
        });

        await foreach (var _ in provider.GetRequiredService<ISender>().CreateStream(new TestStreamRequest(1)))
        {
        }

        trace.Should().Equal("Outer", "Inner");
    }

    private sealed class OuterTracingBehavior<TRequest, TResponse>(List<string> trace) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Add("Outer.before");
            var response = await next(cancellationToken);
            trace.Add("Outer.after");
            return response;
        }
    }

    private sealed class InnerTracingBehavior<TRequest, TResponse>(List<string> trace) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Add("Inner.before");
            var response = await next(cancellationToken);
            trace.Add("Inner.after");
            return response;
        }
    }

    private sealed class ThrowingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
            => throw new InvalidOperationException("behavior failed");
    }

    private sealed class OuterTracingStreamBehavior<TRequest, TResponse>(List<string> trace) : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Add("Outer");
            return next(cancellationToken);
        }
    }

    private sealed class InnerTracingStreamBehavior<TRequest, TResponse>(List<string> trace) : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        public IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Add("Inner");
            return next(cancellationToken);
        }
    }
}
