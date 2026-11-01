using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.Tests;

public sealed class ConfigurationApiTests
{
    private static IServiceCollection EmptyServices() => new ServiceCollection();

    // ---------- AddBehavior closed-generic ----------

    [Fact]
    public void AddBehavior_GenericImpl_RegistersUnderEveryClosedInterface()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg => cfg.AddBehavior<TestClosedPipelineBehavior>());

        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<TestRequest, string>) &&
            x.ImplementationType == typeof(TestClosedPipelineBehavior));
    }

    [Fact]
    public void AddBehavior_GenericServiceAndImpl_RegistersUnderExplicitServiceType()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg =>
            cfg.AddBehavior<IPipelineBehavior<TestRequest, string>, TestClosedPipelineBehavior>());

        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<TestRequest, string>) &&
            x.ImplementationType == typeof(TestClosedPipelineBehavior));
    }

    [Fact]
    public void AddBehavior_TypeOverload_RegistersUnderClosedInterface()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg => cfg.AddBehavior(typeof(TestClosedPipelineBehavior)));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<TestRequest, string>) &&
            x.ImplementationType == typeof(TestClosedPipelineBehavior));
    }

    [Fact]
    public void AddBehavior_WithNonImplementingType_Throws()
    {
        var services = EmptyServices();
        var act = () => services.AddAnyMediator(cfg => cfg.AddBehavior(typeof(TestHandler)));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddBehavior_WithOpenGenericImpl_Throws()
    {
        var services = EmptyServices();
        var act = () => services.AddAnyMediator(cfg => cfg.AddBehavior(typeof(TestPipelineBehavior1<,>)));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddBehavior_ServiceTypeNotClosedGeneric_Throws()
    {
        var services = EmptyServices();
        var act = () => services.AddAnyMediator(cfg =>
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TestClosedPipelineBehavior)));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddBehavior_ImplDoesNotImplementService_Throws()
    {
        var services = EmptyServices();
        var act = () => services.AddAnyMediator(cfg =>
            cfg.AddBehavior(typeof(IPipelineBehavior<TestRequest2, bool>), typeof(TestClosedPipelineBehavior)));
        act.Should().Throw<ArgumentException>();
    }

    // ---------- AddOpenBehaviors bulk ----------

    [Fact]
    public void AddOpenBehaviors_TypeEnumerable_RegistersAllInOrder()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg => cfg.AddOpenBehaviors(new[]
        {
            typeof(TestPipelineBehavior1<,>),
            typeof(TestPipelineBehavior2<,>),
        }));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) && x.ImplementationType == typeof(TestPipelineBehavior1<,>));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) && x.ImplementationType == typeof(TestPipelineBehavior2<,>));
    }

    [Fact]
    public void AddOpenBehaviors_OpenBehaviorEnumerable_HonoursPerItemLifetime()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg => cfg.AddOpenBehaviors(new[]
        {
            new OpenBehavior(typeof(TestPipelineBehavior1<,>), ServiceLifetime.Singleton),
            new OpenBehavior(typeof(TestPipelineBehavior2<,>)), // null → uses cfg.Lifetime (Transient)
        }));

        services.Should().Contain(x =>
            x.ImplementationType == typeof(TestPipelineBehavior1<,>) && x.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(x =>
            x.ImplementationType == typeof(TestPipelineBehavior2<,>) && x.Lifetime == ServiceLifetime.Transient);
    }

    // ---------- AddStreamBehavior closed-generic ----------

    [Fact]
    public void AddStreamBehavior_GenericImpl_RegistersUnderClosedStreamInterface()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg => cfg.AddStreamBehavior<TestClosedStreamPipelineBehavior>());

        services.Should().Contain(x =>
            x.ServiceType == typeof(IStreamPipelineBehavior<TestStreamRequest, int>) &&
            x.ImplementationType == typeof(TestClosedStreamPipelineBehavior));
    }

    [Fact]
    public void AddStreamBehavior_GenericServiceAndImpl_RegistersUnderExplicitServiceType()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg =>
            cfg.AddStreamBehavior<IStreamPipelineBehavior<TestStreamRequest, int>, TestClosedStreamPipelineBehavior>());

        services.Should().Contain(x =>
            x.ServiceType == typeof(IStreamPipelineBehavior<TestStreamRequest, int>) &&
            x.ImplementationType == typeof(TestClosedStreamPipelineBehavior));
    }

    // ---------- AddOpenRequestPreProcessor / AddOpenRequestPostProcessor ----------

    [Fact]
    public void AddOpenRequestPreProcessor_RegistersOpenGenericProcessor()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg => cfg.AddOpenRequestPreProcessor(typeof(TestOpenPreProcessor<>)));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPreProcessor<>) &&
            x.ImplementationType == typeof(TestOpenPreProcessor<>));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType != null && x.ImplementationType.Name.StartsWith("RequestPreProcessorBehavior"));
    }

    [Fact]
    public void AddOpenRequestPostProcessor_RegistersOpenGenericProcessor()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg => cfg.AddOpenRequestPostProcessor(typeof(TestOpenPostProcessor<,>)));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPostProcessor<,>) &&
            x.ImplementationType == typeof(TestOpenPostProcessor<,>));
        services.Should().Contain(x =>
            x.ServiceType == typeof(IPipelineBehavior<,>) &&
            x.ImplementationType != null && x.ImplementationType.Name.StartsWith("RequestPostProcessorBehavior"));
    }

    [Fact]
    public void AddOpenRequestPreProcessor_WithClosedType_Throws()
    {
        var services = EmptyServices();
        var act = () => services.AddAnyMediator(cfg =>
            cfg.AddOpenRequestPreProcessor(typeof(TestPreProcessor1)));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task AddOpenRequestPreProcessor_RunsForEveryRequestType()
    {
        var trace = new List<string>();
        var services = EmptyServices();
        services.AddSingleton(trace);
        services.AddAnyMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ConfigurationApiTests>();
            cfg.AddOpenRequestPreProcessor(typeof(TestOpenPreProcessor<>));
        });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new TestRequest());
        await sender.Send(new TestRequest2(true));

        trace.Should().Contain("OpenPre:TestRequest");
        trace.Should().Contain("OpenPre:TestRequest2");
    }

    // ---------- Two-type-arg processor overloads ----------

    [Fact]
    public void AddRequestPreProcessor_GenericServiceAndImpl_RegistersUnderExplicitServiceType()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg =>
            cfg.AddRequestPreProcessor<IRequestPreProcessor<TestRequest>, TestPreProcessor1>());

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPreProcessor<TestRequest>) &&
            x.ImplementationType == typeof(TestPreProcessor1));
    }

    [Fact]
    public void AddRequestPostProcessor_GenericServiceAndImpl_RegistersUnderExplicitServiceType()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg =>
            cfg.AddRequestPostProcessor<IRequestPostProcessor<TestRequest, string>, TestPostProcessor>());

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPostProcessor<TestRequest, string>) &&
            x.ImplementationType == typeof(TestPostProcessor));
    }

    [Fact]
    public void AddRequestPreProcessor_TypeAndType_RegistersUnderExplicitServiceType()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg =>
            cfg.AddRequestPreProcessor(typeof(IRequestPreProcessor<TestRequest>), typeof(TestPreProcessor1)));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestPreProcessor<TestRequest>) &&
            x.ImplementationType == typeof(TestPreProcessor1));
    }

    // ---------- NotificationPublisher instance ----------

    [Fact]
    public void NotificationPublisher_Instance_TakesPrecedenceOverType()
    {
        var captured = new CapturingNotificationPublisher();
        var services = EmptyServices();
        services.AddAnyMediator(cfg =>
        {
            cfg.NotificationPublisher = captured;
            cfg.NotificationPublisherType = typeof(NotificationPublishers.TaskWhenAllPublisher);
        });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<INotificationPublisher>().Should().BeSameAs(captured);
    }

    [Fact]
    public void NotificationPublisher_NotSet_FallsBackToType()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg =>
            cfg.NotificationPublisherType = typeof(NotificationPublishers.TaskWhenAllPublisher));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<INotificationPublisher>()
            .Should().BeOfType<NotificationPublishers.TaskWhenAllPublisher>();
    }

    private sealed class CapturingNotificationPublisher : INotificationPublisher
    {
        public Task Publish(IEnumerable<Func<Task>> handlerCalls, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    // ---------- RegisterServicesFromAssemblyContaining(Type) ----------

    [Fact]
    public void RegisterServicesFromAssemblyContaining_TypeOverload_ScansAssembly()
    {
        var services = EmptyServices();
        services.AddAnyMediator(cfg =>
            cfg.RegisterServicesFromAssemblyContaining(typeof(ConfigurationApiTests)));

        services.Should().Contain(x =>
            x.ServiceType == typeof(IRequestHandler<TestRequest, string>) &&
            x.ImplementationType == typeof(TestHandler));
    }
}
