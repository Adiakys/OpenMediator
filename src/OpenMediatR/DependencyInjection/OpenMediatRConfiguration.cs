using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR;

public sealed class OpenMediatRConfiguration
{
    internal List<Assembly> AssembliesToRegister { get; } = [];
    internal List<ServiceDescriptor> BehaviorsToRegister { get; } = [];

    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// When <c>true</c>, the assembly scan registers <see cref="IRequestPreProcessor{TRequest}"/> and
    /// <see cref="IRequestPostProcessor{TRequest, TResponse}"/> implementations and wires the matching
    /// pipeline-behavior wrappers automatically. Default is <c>false</c> — register processors and the
    /// wrapper behaviors explicitly to opt in. Mirrors MediatR's behaviour.
    /// </summary>
    public bool AutoRegisterRequestProcessors { get; set; } = false;

    /// <summary>
    /// The notification publisher type that determines how notification handlers are executed.
    /// Available built-in strategies:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see cref="NotificationPublishers.ForeachAwaitPublisher"/> (default) — runs handlers sequentially,
    /// awaiting each one. Stops at the first exception (fail-fast); remaining handlers do not run.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="NotificationPublishers.TaskWhenAllPublisher"/> — runs all handlers in parallel via
    /// <see cref="System.Threading.Tasks.Task.WhenAll(System.Threading.Tasks.Task[])"/>. Failures surface
    /// as the first faulted task's exception; <see cref="AggregateException.InnerExceptions"/> on the
    /// returned task contain every failure.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="NotificationPublishers.ForeachAwaitContinueOnExceptionPublisher"/> — runs handlers
    /// sequentially like the default, but does not stop on failure: every handler executes, and any
    /// captured exceptions are thrown together as a single <see cref="AggregateException"/> at the end.
    /// </description>
    /// </item>
    /// </list>
    /// Custom strategies can be supplied by implementing <see cref="INotificationPublisher"/>.
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
