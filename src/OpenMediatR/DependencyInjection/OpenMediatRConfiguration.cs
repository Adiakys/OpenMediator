using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR;

/// <summary>
/// Fluent configuration consumed by <see cref="ServiceCollectionExtensions.AddOpenMediatR"/>.
/// Splits into partial files by concern: behaviors (this + Behaviors), processors (Processors).
/// </summary>
public sealed partial class OpenMediatRConfiguration
{
    internal List<Assembly> AssembliesToRegister { get; } = [];
    internal List<ServiceDescriptor> BehaviorsToRegister { get; } = [];
    internal List<ServiceDescriptor> StreamBehaviorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestExceptionHandlersToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestExceptionActionsToRegister { get; } = [];

    /// <summary>Default lifetime for handlers, processors, and behaviors that don't specify one.</summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Controls when <see cref="IRequestExceptionAction{TRequest, TException}"/> implementations fire
    /// relative to <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>.
    /// Default is <see cref="OpenMediatR.RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions"/>.
    /// </summary>
    public RequestExceptionActionProcessorStrategy RequestExceptionActionProcessorStrategy { get; set; }
        = RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

    /// <summary>
    /// Optional explicit <see cref="INotificationPublisher"/> instance. When set, takes precedence over
    /// <see cref="NotificationPublisherType"/>. Useful for publishers that need configuration not
    /// expressible through DI alone.
    /// </summary>
    public INotificationPublisher? NotificationPublisher { get; set; }

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

    /// <summary>
    /// Scans the assembly containing <typeparamref name="T"/> for request, stream request and notification
    /// handlers, exception handlers and exception actions (plus pre/post-processors when
    /// <see cref="AutoRegisterRequestProcessors"/> is enabled) and registers them.
    /// </summary>
    /// <typeparam name="T">Any type declared in the assembly to scan.</typeparam>
    public OpenMediatRConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Scans the assembly containing <paramref name="type"/>; see <see cref="RegisterServicesFromAssemblyContaining{T}"/>.
    /// </summary>
    /// <param name="type">Any type declared in the assembly to scan.</param>
    public OpenMediatRConfiguration RegisterServicesFromAssemblyContaining(Type type)
        => RegisterServicesFromAssembly(type.Assembly);

    /// <summary>
    /// Scans <paramref name="assembly"/>; see <see cref="RegisterServicesFromAssemblyContaining{T}"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    public OpenMediatRConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);
        return this;
    }

    /// <summary>
    /// Scans every assembly in <paramref name="assemblies"/>; see <see cref="RegisterServicesFromAssemblyContaining{T}"/>.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    public OpenMediatRConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        AssembliesToRegister.AddRange(assemblies);
        return this;
    }

    private static void EnsureNotOpenGeneric(Type type, string paramName)
    {
        if (type.IsGenericTypeDefinition)
            throw new ArgumentException($"{type.Name} cannot be an open generic type.", paramName);
    }

    private static void EnsureOpenGeneric(Type type, string paramName)
    {
        if (!type.IsGenericTypeDefinition)
            throw new ArgumentException($"{type.Name} must be an open generic type.", paramName);
    }

    private static IReadOnlyList<Type> FindClosedGenericInterfaces(Type type, Type genericTypeDefinition)
    {
        return type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericTypeDefinition)
            .ToArray();
    }

    private static void EnsureImplementsGenericInterface(Type type, Type genericTypeDefinition, string paramName)
    {
        if (FindClosedGenericInterfaces(type, genericTypeDefinition).Count == 0)
            throw new ArgumentException(
                $"{type.Name} must implement {genericTypeDefinition.Name}.", paramName);
    }

    private static void EnsureClosedGenericServiceType(Type serviceType, Type expectedGenericDefinition, string paramName)
    {
        if (!serviceType.IsGenericType ||
            serviceType.IsGenericTypeDefinition ||
            serviceType.GetGenericTypeDefinition() != expectedGenericDefinition)
        {
            throw new ArgumentException(
                $"{serviceType.Name} must be a closed generic of {expectedGenericDefinition.Name}.", paramName);
        }
    }

    private static void EnsureAssignable(Type serviceType, Type implementationType, string paramName)
    {
        if (!serviceType.IsAssignableFrom(implementationType))
            throw new ArgumentException(
                $"{implementationType.Name} must implement {serviceType.Name}.", paramName);
    }

    /// <summary>
    /// Closed-generic registration from an implementation type. Finds every closed
    /// <paramref name="genericInterface"/> the implementation declares and adds one descriptor per match.
    /// </summary>
    private static void AddClosedFromImplementation(
        List<ServiceDescriptor> destination,
        Type genericInterface,
        Type implementationType,
        ServiceLifetime? serviceLifetime)
    {
        EnsureNotOpenGeneric(implementationType, nameof(implementationType));
        var serviceTypes = FindClosedGenericInterfaces(implementationType, genericInterface);
        if (serviceTypes.Count == 0)
            throw new ArgumentException(
                $"{implementationType.Name} must implement {genericInterface.Name}.", nameof(implementationType));

        foreach (var serviceType in serviceTypes)
            destination.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime ?? ServiceLifetime.Transient));
    }

    /// <summary>
    /// Closed-generic registration with an explicit <paramref name="serviceType"/> (closed generic) and
    /// implementation type. Validates the service type is the right closed shape and the implementation
    /// is assignable.
    /// </summary>
    private static void AddClosedWithExplicitService(
        List<ServiceDescriptor> destination,
        Type genericInterface,
        Type serviceType,
        Type implementationType,
        ServiceLifetime? serviceLifetime)
    {
        EnsureClosedGenericServiceType(serviceType, genericInterface, nameof(serviceType));
        EnsureNotOpenGeneric(implementationType, nameof(implementationType));
        EnsureAssignable(serviceType, implementationType, nameof(implementationType));

        destination.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime ?? ServiceLifetime.Transient));
    }

    /// <summary>
    /// Open-generic registration. Validates the open generic shape and that it implements the expected
    /// interface, then registers under the open generic interface itself.
    /// </summary>
    private void AddOpenGenericRegistration(
        List<ServiceDescriptor> destination,
        Type genericInterface,
        Type openType,
        ServiceLifetime? serviceLifetime)
    {
        EnsureOpenGeneric(openType, nameof(openType));
        EnsureImplementsGenericInterface(openType, genericInterface, nameof(openType));

        destination.Add(new ServiceDescriptor(genericInterface, openType, serviceLifetime ?? Lifetime));
    }
}
