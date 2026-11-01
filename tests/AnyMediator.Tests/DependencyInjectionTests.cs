using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using AnyMediator.NotificationPublishers;
using AnyMediator.NotificationSinks;
using AnyMediator.RequestProcessors;

namespace AnyMediator.Tests;

public sealed class DependencyInjectionTests
{
    private static IServiceCollection CreateServices(Action<AnyMediatorConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddAnyMediator(configure ?? (cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddOpenBehavior(typeof(TestPipelineBehavior1<,>));
            cfg.AddOpenBehavior(typeof(TestPipelineBehavior2<,>));
        }));
        return services;
    }

    [Fact]
    public void AddAnyMediator_ShouldRegisterRequestHandlersFromAssembly()
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
    public void AddAnyMediator_ShouldRegisterNotificationHandlersFromAssembly()
    {
        var services = CreateServices();

        services.Should().Contain(x =>
            x.ServiceType == typeof(INotificationHandler<TestNotification>) &&
            x.ImplementationType == typeof(TestHandler));
    }

    [Fact]
    public void AddAnyMediator_ShouldRegisterExplicitPipelineBehaviors()
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
    public void AddAnyMediator_ShouldRegisterCoreServices()
    {
        var services = CreateServices();

        services.Should().Contain(x => x.ServiceType == typeof(ISender) && x.ImplementationType == typeof(AnyMediatorSender));
        services.Should().Contain(x => x.ServiceType == typeof(IPublisher) && x.ImplementationType == typeof(AnyMediatorPublisher));
        services.Should().Contain(x => x.ServiceType == typeof(IMediator) && x.ImplementationType == typeof(Mediator));
        services.Should().Contain(x => x.ServiceType == typeof(INotificationPublisher) && x.ImplementationType == typeof(ForeachAwaitPublisher));
        services.Should().Contain(x => x.ServiceType == typeof(INotificationSink) && x.ImplementationType == typeof(InMemoryNotificationSink));
    }

    [Fact]
    public void AddAnyMediator_ShouldRegisterStreamRequestHandlersFromAssembly()
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
    public void AddAnyMediator_RegistersBuiltInNotificationPublisherAsSingletonAndCustomOneWithConfiguredLifetime()
    {
        CreateServices().Should().Contain(x =>
            x.ServiceType == typeof(INotificationPublisher) && x.Lifetime == ServiceLifetime.Singleton);

        CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.NotificationPublisherType = typeof(CustomNotificationPublisher);
        }).Should().Contain(x =>
            x.ServiceType == typeof(INotificationPublisher) && x.Lifetime == ServiceLifetime.Transient);
    }

    private sealed class CustomNotificationPublisher : INotificationPublisher
    {
        public Task Publish(IEnumerable<Func<Task>> handlerCalls, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void AddAnyMediator_WithCustomNotificationPublisher_ShouldRegisterIt()
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
    public void AddAnyMediator_WithNoAssemblies_DoesNotThrowAndRegistersCoreServices()
    {
        var services = new ServiceCollection();

        var act = () => services.AddAnyMediator(_ => { });

        act.Should().NotThrow();
        services.Should().Contain(x => x.ServiceType == typeof(ISender));
        services.Should().Contain(x => x.ServiceType == typeof(IPublisher));
        services.Should().Contain(x => x.ServiceType == typeof(IMediator));
    }

    [Fact]
    public void AddAnyMediator_ShouldRegisterVoidRequestHandlersFromAssembly()
    {
        var services = CreateServices();

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestHandler<TestVoidRequest, Unit>) &&
            x.ImplementationType == typeof(TestVoidHandler));
    }

    [Fact]
    public void AddAnyMediator_ShouldNotRegisterNonMediatRInterfaces()
    {
        var services = CreateServices();

        services.Should().NotContain(x => x.ServiceType == typeof(IDisposable));
    }

    [Fact]
    public void AddAnyMediator_ByDefault_DoesNotAutoRegisterRequestProcessors()
    {
        var services = CreateServices();

        services.Should().NotContain(x =>
            x.ServiceType == typeof(IRequestPreProcessor<TestRequest>));
        services.Should().NotContain(x =>
            x.ServiceType == typeof(IRequestPostProcessor<TestRequest, string>));
        services.Should().NotContain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestPreProcessorBehavior<,>));
        services.Should().NotContain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestPostProcessorBehavior<,>));
    }

    [Fact]
    public void AddAnyMediator_WhenAutoRegisterEnabled_RegistersPreAndPostProcessors()
    {
        var services = CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AutoRegisterRequestProcessors = true;
        });

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPreProcessor<TestRequest>) &&
            x.ImplementationType == typeof(TestPreProcessor1));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPostProcessor<TestRequest, string>) &&
            x.ImplementationType == typeof(TestPostProcessor));
    }

    [Fact]
    public void AddAnyMediator_WhenAutoRegisterEnabled_AutoRegistersWrapperBehaviors()
    {
        var services = CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AutoRegisterRequestProcessors = true;
        });

        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestPreProcessorBehavior<,>));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestPostProcessorBehavior<,>));
    }

    [Fact]
    public void AddAnyMediator_AutoRegistersExceptionHandlersAndActionsFromAssembly()
    {
        var services = CreateServices();

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestExceptionHandler<TestRequest, string, InvalidOperationException>) &&
            x.ImplementationType == typeof(TestExceptionHandler));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestExceptionAction<TestRequest, Exception>) &&
            x.ImplementationType == typeof(TestExceptionAction));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestExceptionProcessorBehavior<,>));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestExceptionActionProcessorBehavior<,>));
    }

    [Fact]
    public void AddRequestExceptionHandler_RegistersHandlerAndWrapperBehavior()
    {
        var services = new ServiceCollection();
        services.AddAnyMediator(cfg => cfg.AddRequestExceptionHandler<TestExceptionHandler>());

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestExceptionHandler<TestRequest, string, InvalidOperationException>) &&
            x.ImplementationType == typeof(TestExceptionHandler));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestExceptionProcessorBehavior<,>));
    }

    [Fact]
    public void AddRequestExceptionAction_RegistersActionAndWrapperBehavior()
    {
        var services = new ServiceCollection();
        services.AddAnyMediator(cfg => cfg.AddRequestExceptionAction<TestExceptionAction>());

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestExceptionAction<TestRequest, Exception>) &&
            x.ImplementationType == typeof(TestExceptionAction));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestExceptionActionProcessorBehavior<,>));
    }

    [Fact]
    public void AddRequestExceptionHandler_WithNonImplementingType_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddAnyMediator(cfg => cfg.AddRequestExceptionHandler(typeof(TestHandler)));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRequestExceptionAction_WithOpenGenericType_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddAnyMediator(cfg => cfg.AddRequestExceptionAction(typeof(List<>)));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRequestPreProcessor_RegistersProcessorAndWrapperBehavior()
    {
        var services = CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddRequestPreProcessor<TestPreProcessor1>();
        });

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPreProcessor<TestRequest>) &&
            x.ImplementationType == typeof(TestPreProcessor1));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestPreProcessorBehavior<,>));
    }

    [Fact]
    public void AddRequestPostProcessor_RegistersProcessorAndWrapperBehavior()
    {
        var services = CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddRequestPostProcessor<TestPostProcessor>();
        });

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPostProcessor<TestRequest, string>) &&
            x.ImplementationType == typeof(TestPostProcessor));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestPostProcessorBehavior<,>));
    }

    [Fact]
    public void AddRequestPreProcessor_WithNonImplementingType_Throws()
    {
        var act = () => CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddRequestPreProcessor(typeof(TestHandler));
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRequestPostProcessor_WithOpenGenericType_Throws()
    {
        var act = () => CreateServices(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddRequestPostProcessor(typeof(List<>));
        });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddAnyMediator_CalledTwice_ShouldNotDuplicateScannedHandlersAndProcessors()
    {
        var services = new ServiceCollection();

        Action<AnyMediatorConfiguration> configure = cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AutoRegisterRequestProcessors = true;
        };

        services.AddAnyMediator(configure);
        services.AddAnyMediator(configure);

        services.Where(x =>
            x.ServiceType == typeof(INotificationHandler<TestNotification>) &&
            x.ImplementationType == typeof(TestHandler))
            .Should().HaveCount(1);

        services.Where(x =>
            x.ServiceType == typeof(IRequestPreProcessor<TestRequest>) &&
            x.ImplementationType == typeof(TestPreProcessor1))
            .Should().HaveCount(1);

        services.Where(x =>
            x.ServiceType == typeof(IRequestPostProcessor<TestRequest, string>) &&
            x.ImplementationType == typeof(TestPostProcessor))
            .Should().HaveCount(1);

        services.Where(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(RequestPreProcessorBehavior<,>))
            .Should().HaveCount(1);
    }

    [Fact]
    public void AddAnyMediator_CalledTwice_ShouldNotDuplicateUserBehaviors()
    {
        var services = new ServiceCollection();

        Action<AnyMediatorConfiguration> configure = cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
            cfg.AddOpenBehavior(typeof(TestPipelineBehavior1<,>));
            cfg.AddOpenBehavior(typeof(TestPipelineBehavior2<,>));
        };

        services.AddAnyMediator(configure);
        services.AddAnyMediator(configure);

        services.Where(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(TestPipelineBehavior1<,>))
            .Should().HaveCount(1);

        services.Where(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType == typeof(TestPipelineBehavior2<,>))
            .Should().HaveCount(1);
    }

    [Fact]
    public void AddAnyMediator_CalledTwice_ShouldNotDuplicateCoreServices()
    {
        var services = new ServiceCollection();

        services.AddAnyMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
        });

        services.AddAnyMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DependencyInjectionTests>();
        });

        services.Where(x => x.ServiceType == typeof(ISender)).Should().HaveCount(1);
        services.Where(x => x.ServiceType == typeof(IPublisher)).Should().HaveCount(1);
        services.Where(x => x.ServiceType == typeof(IMediator)).Should().HaveCount(1);
    }
}
