using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMediatR.NotificationPublishers;
using OpenMediatR.NotificationSinks;

namespace OpenMediatR.Tests;

public sealed class DependencyInjectionTests
{
    private static IServiceCollection CreateServices(Action<OpenMediatRConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddOpenMediatR(configure ?? (cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddOpenBehavior(typeof(TestPipelineBehavior1<,>));
            cfg.AddOpenBehavior(typeof(TestPipelineBehavior2<,>));
        }));
        return services;
    }

    [Fact]
    public void AddOpenMediatR_ShouldRegisterRequestHandlersFromAssembly()
    {
        var services = CreateServices();

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestHandler<TestRequest, string>) &&
            x.ImplementationType == typeof(TestHandler));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestHandler<TestRequest2, bool>) &&
            x.ImplementationType == typeof(TestHandler));
    }

    [Fact]
    public void AddOpenMediatR_ShouldRegisterNotificationHandlersFromAssembly()
    {
        var services = CreateServices();

        services.Should().Contain(x =>
            x.ServiceType == typeof(INotificationHandler<TestNotification>) &&
            x.ImplementationType == typeof(TestHandler));
    }

    [Fact]
    public void AddOpenMediatR_ShouldRegisterExplicitPipelineBehaviors()
    {
        var services = CreateServices();

        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(TestPipelineBehavior1<,>));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(TestPipelineBehavior2<,>));
    }

    [Fact]
    public void AddOpenMediatR_ShouldRegisterCoreServices()
    {
        var services = CreateServices();

        services.Should().Contain(x => x.ServiceType == typeof(ISender) && x.ImplementationType == typeof(OpenMediatRSender));
        services.Should().Contain(x => x.ServiceType == typeof(IPublisher) && x.ImplementationType == typeof(OpenMediatRPublisher));
        services.Should().Contain(x => x.ServiceType == typeof(IMediator) && x.ImplementationType == typeof(Mediator));
        services.Should().Contain(x => x.ServiceType == typeof(INotificationPublisher) && x.ImplementationType == typeof(ForeachAwaitPublisher));
        services.Should().Contain(x => x.ServiceType == typeof(INotificationSink) && x.ImplementationType == typeof(InMemoryNotificationSink));
    }

    [Fact]
    public void AddOpenMediatR_ShouldRegisterStreamRequestHandlersFromAssembly()
    {
        var services = CreateServices();

        services.Should().Contain(x =>
            x.ServiceType == typeof(IStreamRequestHandler<TestStreamRequest, int>) &&
            x.ImplementationType == typeof(TestStreamHandler));
    }

    [Fact]
    public void AddOpenStreamBehavior_RegistersStreamPipelineBehavior()
    {
        var services = CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddOpenStreamBehavior(typeof(TestStreamPipelineBehavior<,>));
        });

        services.Should().Contain(x =>
            x.ServiceType == typeof(IStreamPipelineBehavior<,>) &&
            x.ImplementationType == typeof(TestStreamPipelineBehavior<,>));
    }

    [Fact]
    public void AddOpenStreamBehavior_WithNonGenericType_ShouldThrow()
    {
        var act = () => CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddOpenStreamBehavior(typeof(string));
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddOpenStreamBehavior_WithTypeThatDoesNotImplementStreamPipelineBehavior_ShouldThrow()
    {
        var act = () => CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddOpenStreamBehavior(typeof(List<>));
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddOpenMediatR_WithCustomNotificationPublisher_ShouldRegisterIt()
    {
        var services = CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
        });

        services.Should().Contain(x =>
            x.ServiceType == typeof(INotificationPublisher) &&
            x.ImplementationType == typeof(TaskWhenAllPublisher));
    }

    [Fact]
    public void AddOpenBehavior_WithNonGenericType_ShouldThrow()
    {
        var act = () => CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddOpenBehavior(typeof(string));
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddOpenBehavior_WithTypeThatDoesNotImplementIPipelineBehavior_ShouldThrow()
    {
        var act = () => CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddOpenBehavior(typeof(List<>));
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddOpenMediatR_WithNoAssemblies_ShouldThrow()
    {
        var services = new ServiceCollection();

        var act = () => services.AddOpenMediatR(_ => { });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddOpenMediatR_ShouldRegisterVoidRequestHandlersFromAssembly()
    {
        var services = CreateServices();

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestHandler<TestVoidRequest, Unit>) &&
            x.ImplementationType == typeof(TestVoidHandler));
    }

    [Fact]
    public void AddOpenMediatR_ShouldNotRegisterNonMediatRInterfaces()
    {
        var services = CreateServices();

        services.Should().NotContain(x => x.ServiceType == typeof(IDisposable));
    }

    [Fact]
    public void AddOpenMediatR_CalledTwice_ShouldNotDuplicateCoreServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddOpenMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
        });

        services.AddOpenMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
        });

        services.Where(x => x.ServiceType == typeof(ISender)).Should().HaveCount(1);
        services.Where(x => x.ServiceType == typeof(IPublisher)).Should().HaveCount(1);
        services.Where(x => x.ServiceType == typeof(IMediator)).Should().HaveCount(1);
    }
}
