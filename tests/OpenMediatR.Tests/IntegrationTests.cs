using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenMediatR.Tests;

public sealed class IntegrationTests
{
    private static ServiceProvider BuildProvider(Action<OpenMediatRConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddOpenMediatR(configure ?? (cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IntegrationTests>();
            cfg.AddOpenBehavior(typeof(TestPipelineBehavior1<,>));
            cfg.AddOpenBehavior(typeof(TestPipelineBehavior2<,>));
        }));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_WithRealDI_ShouldResolveHandlerAndReturnResult()
    {
        using var provider = BuildProvider(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<IntegrationTests>());

        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new TestRequest());

        result.Should().Be("Test");
    }

    [Fact]
    public async Task Send_WithRealDI_ShouldExecutePipelineBehaviors()
    {
        using var provider = BuildProvider();

        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new TestRequest());

        result.Should().Be("Test");
    }

    [Fact]
    public async Task Send_DifferentRequestTypes_ShouldResolveCorrectHandler()
    {
        using var provider = BuildProvider(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<IntegrationTests>());

        var sender = provider.GetRequiredService<ISender>();

        var stringResult = await sender.Send(new TestRequest());
        var boolResult = await sender.Send(new TestRequest2(true));

        stringResult.Should().Be("Test");
        boolResult.Should().BeTrue();
    }

    [Fact]
    public async Task Publish_WithRealDI_ShouldDispatchToHandlers()
    {
        using var provider = BuildProvider(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<IntegrationTests>());

        var publisher = provider.GetRequiredService<IPublisher>();

        var act = () => publisher.Publish(new TestNotification());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Mediator_WithRealDI_ShouldDelegateSendAndPublish()
    {
        using var provider = BuildProvider(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<IntegrationTests>());

        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestRequest());
        result.Should().Be("Test");

        var act = () => mediator.Publish(new TestNotification());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateStream_WithRealDI_ResolvesHandlerAndYieldsValues()
    {
        using var provider = BuildProvider(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<IntegrationTests>());

        var sender = provider.GetRequiredService<ISender>();

        var values = new List<int>();
        await foreach (var value in sender.CreateStream(new TestStreamRequest(4)))
            values.Add(value);

        values.Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public async Task Mediator_WithRealDI_DelegatesCreateStream()
    {
        using var provider = BuildProvider(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IntegrationTests>();
            cfg.AddOpenStreamBehavior(typeof(TestStreamPipelineBehavior<,>));
        });

        var mediator = provider.GetRequiredService<IMediator>();

        var values = new List<int>();
        await foreach (var value in mediator.CreateStream(new TestStreamRequest(2)))
            values.Add(value);

        values.Should().Equal(0, 1);
    }

    [Fact]
    public async Task Send_WithoutRegisteredHandler_ThrowsActionableMessage()
    {
        using var provider = BuildProvider();

        var sender = provider.GetRequiredService<ISender>();

        var act = () => sender.Send(new UnhandledRequest());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{typeof(UnhandledRequest).FullName}*IRequestHandler<UnhandledRequest, Int32>*RegisterServicesFromAssemblyContaining*");
    }

    private sealed record UnhandledRequest : IRequest<int>;

    [Fact]
    public async Task Send_WithBehaviorCallingNextWithoutToken_ReachesHandler()
    {
        using var provider = BuildProvider(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IntegrationTests>();
            cfg.AddOpenBehavior(typeof(ParameterlessNextBehavior<,>));
        });

        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new TestRequest());

        result.Should().Be("Test");
    }

    private sealed class ParameterlessNextBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
            => await next();
    }
}
