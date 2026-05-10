using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR;

public sealed class OpenMediatRConfiguration
{
    internal List<Assembly> AssembliesToRegister { get; } = [];
    internal List<ServiceDescriptor> BehaviorsToRegister { get; } = [];

    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// The notification publisher type that determines how notification handlers are executed.
    /// Defaults to <see cref="NotificationPublishers.ForeachAwaitPublisher"/> (sequential).
    /// Use <see cref="NotificationPublishers.TaskWhenAllPublisher"/> for parallel execution.
    /// </summary>
    public Type NotificationPublisherType { get; set; } = typeof(NotificationPublishers.ForeachAwaitPublisher);

    public OpenMediatRConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssembly(typeof(T).Assembly);

    public OpenMediatRConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);
        return this;
    }

    public OpenMediatRConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        AssembliesToRegister.AddRange(assemblies);
        return this;
    }

    public OpenMediatRConfiguration AddOpenBehavior(Type openBehaviorType, ServiceLifetime? lifetime = null)
    {
        if (!openBehaviorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"{openBehaviorType.Name} must be an open generic type.", nameof(openBehaviorType));

        var implementsPipeline = openBehaviorType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        if (!implementsPipeline)
            throw new ArgumentException(
                $"{openBehaviorType.Name} must implement {typeof(IPipelineBehavior<,>).Name}.", nameof(openBehaviorType));

        BehaviorsToRegister.Add(new ServiceDescriptor(
            typeof(IPipelineBehavior<,>), openBehaviorType, lifetime ?? Lifetime));

        return this;
    }

    public OpenMediatRConfiguration AddOpenStreamBehavior(Type openBehaviorType, ServiceLifetime? lifetime = null)
    {
        if (!openBehaviorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"{openBehaviorType.Name} must be an open generic type.", nameof(openBehaviorType));

        var implementsPipeline = openBehaviorType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamPipelineBehavior<,>));

        if (!implementsPipeline)
            throw new ArgumentException(
                $"{openBehaviorType.Name} must implement {typeof(IStreamPipelineBehavior<,>).Name}.", nameof(openBehaviorType));

        BehaviorsToRegister.Add(new ServiceDescriptor(
            typeof(IStreamPipelineBehavior<,>), openBehaviorType, lifetime ?? Lifetime));

        return this;
    }
}
