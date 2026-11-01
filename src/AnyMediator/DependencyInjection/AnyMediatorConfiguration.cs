using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator;

/// <summary>
/// Fluent configuration consumed by <see cref="ServiceCollectionExtensions.AddAnyMediator"/>.
/// Splits into partial files by concern: behaviors (this + Behaviors), processors (Processors).
/// </summary>
public sealed partial class AnyMediatorConfiguration
{
    internal List<Assembly> AssembliesToRegister { get; } = [];
    internal List<ServiceDescriptor> BehaviorsToRegister { get; } = [];
    internal List<ServiceDescriptor> StreamBehaviorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestExceptionHandlersToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestExceptionActionsToRegister { get; } = [];

    /// <summary>
    /// Decides which scanned types are registered: return <see langword="false"/> to leave a type out, for example a
    /// handler that only a test project should use. Default: every type. Same name and role as in MediatR.
    /// </summary>
    public Func<Type, bool> TypeEvaluator { get; set; } = _ => true;

    /// <summary>Default lifetime for handlers, processors, and behaviors that don't specify one.</summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Controls when <see cref="IRequestExceptionAction{TRequest, TException}"/> implementations fire
    /// relative to <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>.
    /// Default is <see cref="AnyMediator.RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions"/>.
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
    public AnyMediatorConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Scans the assembly containing <paramref name="type"/>; see <see cref="RegisterServicesFromAssemblyContaining{T}"/>.
    /// </summary>
    /// <param name="type">Any type declared in the assembly to scan.</param>
    public AnyMediatorConfiguration RegisterServicesFromAssemblyContaining(Type type)
        => RegisterServicesFromAssembly(type.Assembly);

    /// <summary>
    /// Scans <paramref name="assembly"/>; see <see cref="RegisterServicesFromAssemblyContaining{T}"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    public AnyMediatorConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);
        return this;
    }

    /// <summary>
    /// Scans every assembly in <paramref name="assemblies"/>; see <see cref="RegisterServicesFromAssemblyContaining{T}"/>.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    public AnyMediatorConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        AssembliesToRegister.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Closed-generic registration from an implementation type: one descriptor per closed
    /// <paramref name="genericInterface"/> the implementation declares.
    /// </summary>
    private AnyMediatorConfiguration AddClosedFromImplementation(
        List<ServiceDescriptor> destination, Type genericInterface, Type implementationType, ServiceLifetime? serviceLifetime)
    {
        ThrowIfOpenGeneric(implementationType);

        var serviceTypes = implementationType.GetClosedInterfaces(genericInterface);
        if (serviceTypes.Length == 0)
            throw new ArgumentException($"{implementationType.Name} must implement {genericInterface.Name}.", nameof(implementationType));

        destination.AddRange(serviceTypes.Select(t => new ServiceDescriptor(t, implementationType, serviceLifetime ?? ServiceLifetime.Transient)));
        return this;
    }

    /// <summary>
    /// Closed-generic registration under an explicit closed <paramref name="serviceType"/> of
    /// <paramref name="genericInterface"/>, which <paramref name="implementationType"/> must implement.
    /// </summary>
    private AnyMediatorConfiguration AddClosedWithExplicitService(
        List<ServiceDescriptor> destination, Type genericInterface, Type serviceType, Type implementationType, ServiceLifetime? serviceLifetime)
    {
        if (!serviceType.IsConstructedGenericType || serviceType.GetGenericTypeDefinition() != genericInterface)
            throw new ArgumentException($"{serviceType.Name} must be a closed generic of {genericInterface.Name}.", nameof(serviceType));

        ThrowIfOpenGeneric(implementationType);

        if (!serviceType.IsAssignableFrom(implementationType))
            throw new ArgumentException($"{implementationType.Name} must implement {serviceType.Name}.", nameof(implementationType));

        destination.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime ?? ServiceLifetime.Transient));
        return this;
    }

    /// <summary>Open-generic registration under the open <paramref name="genericInterface"/> itself.</summary>
    private AnyMediatorConfiguration AddOpenGenericRegistration(
        List<ServiceDescriptor> destination, Type genericInterface, Type openType, ServiceLifetime? serviceLifetime)
    {
        if (!openType.IsGenericTypeDefinition)
            throw new ArgumentException($"{openType.Name} must be an open generic type.", nameof(openType));

        if (openType.GetClosedInterfaces(genericInterface).Length == 0)
            throw new ArgumentException($"{openType.Name} must implement {genericInterface.Name}.", nameof(openType));

        destination.Add(new ServiceDescriptor(genericInterface, openType, serviceLifetime ?? Lifetime));
        return this;
    }

    private static void ThrowIfOpenGeneric(Type implementationType)
    {
        if (implementationType.IsGenericTypeDefinition)
            throw new ArgumentException($"{implementationType.Name} cannot be an open generic type.", nameof(implementationType));
    }
}
