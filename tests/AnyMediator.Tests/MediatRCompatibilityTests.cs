using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator.Tests;

public sealed class MediatRCompatibilityTests
{
    private static ServiceProvider BuildProvider(List<string> trace, Action<AnyMediatorConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddAnyMediator(configure ?? (cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRCompatibilityTests>()));
        return services.BuildServiceProvider();
    }

    // ---------- Publish propagates handler failures ----------

    [Fact]
    public async Task Publish_WhenHandlerThrows_ExceptionReachesCaller()
    {
        using var provider = BuildProvider([]);

        var act = () => provider.GetRequiredService<IPublisher>().Publish(new FailingEvent());

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("handler failed");
    }

    // ---------- Behaviors written for MediatR ----------

    [Fact]
    public async Task Send_WithBehaviorConstrainedToNotNull_RunsBehavior()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace, cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<MediatRCompatibilityTests>();
            cfg.AddOpenBehavior(typeof(NotNullBehavior<,>));
        });

        var result = await provider.GetRequiredService<ISender>().Send(new TestRequest());

        result.Should().Be("Test");
        trace.Should().Equal("NotNullBehavior");
    }

    // ---------- Send(object) ----------

    [Fact]
    public async Task SendObject_ReturnsBoxedResponse()
    {
        using var provider = BuildProvider([]);
        object request = new TestRequest();

        var result = await provider.GetRequiredService<ISender>().Send(request);

        result.Should().Be("Test");
    }

    [Fact]
    public async Task SendObject_WithVoidRequest_ReturnsUnit()
    {
        var called = false;
        using var provider = BuildProvider([]);
        object request = new TestVoidRequest(() => called = true);

        var result = await provider.GetRequiredService<IMediator>().Send(request);

        result.Should().Be(Unit.Value);
        called.Should().BeTrue();
    }

    [Fact]
    public async Task SendObject_WhenNotARequest_ThrowsArgumentException()
    {
        using var provider = BuildProvider([]);

        var act = () => provider.GetRequiredService<ISender>().Send((object)"not a request");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendObject_WhenRequestHasSeveralResponseTypes_ThrowsArgumentException()
    {
        using var provider = BuildProvider([]);

        var act = () => provider.GetRequiredService<ISender>().Send((object)new MultiResponseRequest());

        (await act.Should().ThrowAsync<ArgumentException>()).WithMessage("*more than one response type*");
    }

    // ---------- Polymorphic and generic notification handlers ----------

    [Fact]
    public async Task Publish_RunsHandlersOfBaseInterfaceAndGenericHandlers_EachClassOnce()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace);

        await provider.GetRequiredService<IPublisher>().Publish(new OrderShipped(7));

        trace.Should().BeEquivalentTo(
            "OrderShippedHandler:7",
            "ShipmentEventHandler:OrderShipped",
            "ShipmentAuditHandler:OrderShipped");
    }

    [Fact]
    public async Task Publish_RunsHandlerWhoseNotificationIsGeneric()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace);

        await provider.GetRequiredService<IPublisher>().Publish(new EntityCreated<Customer>(new Customer("Ada")));

        trace.Should().Equal("EntityCreatedHandler:Customer");
    }

    [Fact]
    public async Task PublishObject_UsesRuntimeTypeForPolymorphicHandlers()
    {
        var trace = new List<string>();
        using var provider = BuildProvider(trace);
        object notification = new OrderShipped(3);

        await provider.GetRequiredService<IPublisher>().Publish(notification);

        trace.Should().Contain("OrderShippedHandler:3").And.Contain("ShipmentEventHandler:OrderShipped");
    }

    // ---------- Generic request handlers ----------

    [Fact]
    public async Task Send_ResolvesOpenGenericRequestHandler()
    {
        using var provider = BuildProvider([]);

        var customer = await provider.GetRequiredService<ISender>().Send(new GetDefault<Customer>());

        customer.Name.Should().Be("default");
    }

    // ---------- Duplicate handlers ----------

    [Fact]
    public void Registry_WhenTwoClassesHandleTheSameRequest_Throws()
    {
        var registry = new HandlerRegistry();
        registry.AddSingleHandler(typeof(IRequestHandler<TestRequest, string>), typeof(TestHandler));

        var act = () => registry.AddSingleHandler(typeof(IRequestHandler<TestRequest, string>), typeof(SecondTestRequestHandler));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TestRequest*more than one handler*TestHandler*SecondTestRequestHandler*TypeEvaluator*");
    }

    [Fact]
    public void Registry_WhenTheSameClassIsAddedTwice_IgnoresIt()
    {
        var registry = new HandlerRegistry();
        registry.AddSingleHandler(typeof(IRequestHandler<TestRequest, string>), typeof(TestHandler));

        var act = () => registry.AddSingleHandler(typeof(IRequestHandler<TestRequest, string>), typeof(TestHandler));

        act.Should().NotThrow();
    }

    // ---------- TypeEvaluator ----------

    [Fact]
    public async Task TypeEvaluator_ExcludesTypesFromScanning()
    {
        using var provider = BuildProvider([], cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<MediatRCompatibilityTests>();
            cfg.TypeEvaluator = t => t != typeof(TestHandler);
        });

        var act = () => provider.GetRequiredService<ISender>().Send(new TestRequest());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No handler registered*TestRequest*");
    }

    // ---------- Fixtures: each uses types of its own, so other tests scanning this assembly are unaffected ----------

    private sealed record FailingEvent : INotification;

    private sealed class FailingEventHandler : INotificationHandler<FailingEvent>
    {
        public Task Handle(FailingEvent notification, CancellationToken cancellationToken)
            => throw new InvalidOperationException("handler failed");
    }

    private sealed class NotNullBehavior<TRequest, TResponse>(List<string> trace) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Add("NotNullBehavior");
            return next();
        }
    }

    private interface IShipmentEvent : INotification;

    private sealed record OrderShipped(int OrderId) : IShipmentEvent;

    private sealed class OrderShippedHandler(List<string> trace) : INotificationHandler<OrderShipped>
    {
        public Task Handle(OrderShipped notification, CancellationToken cancellationToken)
        {
            trace.Add($"OrderShippedHandler:{notification.OrderId}");
            return Task.CompletedTask;
        }
    }

    // Handles every shipment event; also implements the specific interface, and must still run only once.
    private sealed class ShipmentEventHandler(List<string> trace)
        : INotificationHandler<IShipmentEvent>, INotificationHandler<OrderShipped>
    {
        public Task Handle(IShipmentEvent notification, CancellationToken cancellationToken)
        {
            trace.Add($"ShipmentEventHandler:{notification.GetType().Name}");
            return Task.CompletedTask;
        }

        Task INotificationHandler<OrderShipped>.Handle(OrderShipped notification, CancellationToken cancellationToken)
            => Handle((IShipmentEvent)notification, cancellationToken);
    }

    private sealed class ShipmentAuditHandler<TEvent>(List<string> trace) : INotificationHandler<TEvent>
        where TEvent : IShipmentEvent
    {
        public Task Handle(TEvent notification, CancellationToken cancellationToken)
        {
            trace.Add($"ShipmentAuditHandler:{notification.GetType().Name}");
            return Task.CompletedTask;
        }
    }

    private sealed record Customer(string Name);

    private sealed record EntityCreated<TEntity>(TEntity Entity) : INotification;

    private sealed class EntityCreatedHandler<TEntity>(List<string> trace) : INotificationHandler<EntityCreated<TEntity>>
    {
        public Task Handle(EntityCreated<TEntity> notification, CancellationToken cancellationToken)
        {
            trace.Add($"EntityCreatedHandler:{typeof(TEntity).Name}");
            return Task.CompletedTask;
        }
    }

    private sealed record GetDefault<T> : IRequest<T>;

    private sealed class GetDefaultHandler<T> : IRequestHandler<GetDefault<T>, T>
    {
        public Task<T> Handle(GetDefault<T> request, CancellationToken cancellationToken)
            => Task.FromResult((T)(object)new Customer("default"));
    }

    // Not scanned as a handler: only used to exercise the registry directly.
    private abstract class SecondTestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public abstract Task<string> Handle(TestRequest request, CancellationToken cancellationToken);
    }
}
