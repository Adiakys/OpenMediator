using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator;

public sealed partial class AnyMediatorConfiguration
{
    // ---------- Pipeline behaviors (IPipelineBehavior<,>) ----------

    /// <summary>
    /// Registers a closed pipeline behavior under the explicit
    /// <see cref="IPipelineBehavior{TRequest, TResponse}"/> service type it should be resolved as.
    /// Use when an implementation implements multiple closed behavior interfaces and you want to wire
    /// only one of them.
    /// </summary>
    public AnyMediatorConfiguration AddBehavior<TServiceType, TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        where TServiceType : class
        where TImplementationType : class, TServiceType
        => AddBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <inheritdoc cref="AddBehavior{TServiceType, TImplementationType}"/>
    public AnyMediatorConfiguration AddBehavior(
        Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddClosedWithExplicitService(BehaviorsToRegister, typeof(IPipelineBehavior<,>), serviceType, implementationType, serviceLifetime);

    /// <summary>
    /// Registers a closed pipeline behavior under every <see cref="IPipelineBehavior{TRequest, TResponse}"/>
    /// closed interface it implements. Useful when one type handles a single (request, response) pair.
    /// </summary>
    public AnyMediatorConfiguration AddBehavior<TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        where TImplementationType : class
        => AddBehavior(typeof(TImplementationType), serviceLifetime);

    /// <inheritdoc cref="AddBehavior{TImplementationType}"/>
    public AnyMediatorConfiguration AddBehavior(
        Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddClosedFromImplementation(BehaviorsToRegister, typeof(IPipelineBehavior<,>), implementationType, serviceLifetime);

    /// <summary>
    /// Registers an open generic pipeline behavior (e.g. <c>LoggingBehavior&lt;,&gt;</c>) that applies to
    /// every request type. Lifetime defaults to the configuration's <see cref="Lifetime"/>.
    /// </summary>
    public AnyMediatorConfiguration AddOpenBehavior(Type openBehaviorType, ServiceLifetime? serviceLifetime = null)
        => AddOpenGenericRegistration(BehaviorsToRegister, typeof(IPipelineBehavior<,>), openBehaviorType, serviceLifetime);

    /// <summary>
    /// Bulk registration of open generic pipeline behaviors with a shared lifetime.
    /// Behaviors are registered in enumeration order.
    /// </summary>
    public AnyMediatorConfiguration AddOpenBehaviors(
        IEnumerable<Type> openBehaviorTypes, ServiceLifetime? serviceLifetime = null)
    {
        foreach (var openBehaviorType in openBehaviorTypes)
            AddOpenBehavior(openBehaviorType, serviceLifetime);
        return this;
    }

    /// <summary>
    /// Bulk registration of open generic pipeline behaviors with a per-item lifetime override.
    /// </summary>
    public AnyMediatorConfiguration AddOpenBehaviors(IEnumerable<OpenBehavior> openBehaviors)
    {
        foreach (var openBehavior in openBehaviors)
            AddOpenBehavior(openBehavior.OpenBehaviorType, openBehavior.Lifetime);
        return this;
    }

    // ---------- Stream pipeline behaviors (IStreamPipelineBehavior<,>) ----------

    /// <summary>
    /// Registers a closed stream pipeline behavior under the explicit
    /// <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> service type.
    /// </summary>
    public AnyMediatorConfiguration AddStreamBehavior<TServiceType, TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        where TServiceType : class
        where TImplementationType : class, TServiceType
        => AddStreamBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <inheritdoc cref="AddStreamBehavior{TServiceType, TImplementationType}"/>
    public AnyMediatorConfiguration AddStreamBehavior(
        Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddClosedWithExplicitService(StreamBehaviorsToRegister, typeof(IStreamPipelineBehavior<,>), serviceType, implementationType, serviceLifetime);

    /// <summary>
    /// Registers a closed stream pipeline behavior under every
    /// <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> closed interface it implements.
    /// </summary>
    public AnyMediatorConfiguration AddStreamBehavior<TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        where TImplementationType : class
        => AddStreamBehavior(typeof(TImplementationType), serviceLifetime);

    /// <inheritdoc cref="AddStreamBehavior{TImplementationType}"/>
    public AnyMediatorConfiguration AddStreamBehavior(
        Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddClosedFromImplementation(StreamBehaviorsToRegister, typeof(IStreamPipelineBehavior<,>), implementationType, serviceLifetime);

    /// <summary>
    /// Registers an open generic stream pipeline behavior (e.g. <c>StreamLoggingBehavior&lt;,&gt;</c>).
    /// </summary>
    public AnyMediatorConfiguration AddOpenStreamBehavior(Type openBehaviorType, ServiceLifetime? serviceLifetime = null)
        => AddOpenGenericRegistration(StreamBehaviorsToRegister, typeof(IStreamPipelineBehavior<,>), openBehaviorType, serviceLifetime);

    /// <summary>
    /// Bulk registration of open generic stream pipeline behaviors with a shared lifetime.
    /// </summary>
    public AnyMediatorConfiguration AddOpenStreamBehaviors(
        IEnumerable<Type> openBehaviorTypes, ServiceLifetime? serviceLifetime = null)
    {
        foreach (var openBehaviorType in openBehaviorTypes)
            AddOpenStreamBehavior(openBehaviorType, serviceLifetime);
        return this;
    }

    /// <summary>
    /// Bulk registration of open generic stream pipeline behaviors with a per-item lifetime override.
    /// </summary>
    public AnyMediatorConfiguration AddOpenStreamBehaviors(IEnumerable<OpenBehavior> openBehaviors)
    {
        foreach (var openBehavior in openBehaviors)
            AddOpenStreamBehavior(openBehavior.OpenBehaviorType, openBehavior.Lifetime);
        return this;
    }
}
