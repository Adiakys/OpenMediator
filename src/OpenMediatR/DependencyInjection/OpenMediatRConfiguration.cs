using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR;

public sealed class OpenMediatRConfiguration
{
    internal List<Assembly> AssembliesToRegister { get; } = [];
    internal List<ServiceDescriptor> BehaviorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestExceptionHandlersToRegister { get; } = [];
    internal List<ServiceDescriptor> RequestExceptionActionsToRegister { get; } = [];

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

    /// <summary>
    /// Explicitly registers a single <see cref="IRequestPreProcessor{TRequest}"/> implementation.
    /// The pre-processor wrapper behavior is wired automatically. Use this when you do not want to
    /// enable <see cref="AutoRegisterRequestProcessors"/> assembly scanning but still want pre-processors.
    /// </summary>
    public OpenMediatRConfiguration AddRequestPreProcessor<TPreProcessor>(ServiceLifetime? lifetime = null)
        where TPreProcessor : class
        => AddRequestPreProcessor(typeof(TPreProcessor), lifetime);

    public OpenMediatRConfiguration AddRequestPreProcessor(Type preProcessorType, ServiceLifetime? lifetime = null)
    {
        if (preProcessorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"{preProcessorType.Name} cannot be an open generic type.", nameof(preProcessorType));

        var serviceTypes = preProcessorType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestPreProcessor<>))
            .ToList();

        if (serviceTypes.Count == 0)
            throw new ArgumentException(
                $"{preProcessorType.Name} must implement {typeof(IRequestPreProcessor<>).Name}.", nameof(preProcessorType));

        foreach (var serviceType in serviceTypes)
            RequestPreProcessorsToRegister.Add(
                new ServiceDescriptor(serviceType, preProcessorType, lifetime ?? ServiceLifetime.Transient));

        return this;
    }

    /// <summary>
    /// Explicitly registers a single <see cref="IRequestPostProcessor{TRequest, TResponse}"/> implementation.
    /// The post-processor wrapper behavior is wired automatically. Use this when you do not want to
    /// enable <see cref="AutoRegisterRequestProcessors"/> assembly scanning but still want post-processors.
    /// </summary>
    public OpenMediatRConfiguration AddRequestPostProcessor<TPostProcessor>(ServiceLifetime? lifetime = null)
        where TPostProcessor : class
        => AddRequestPostProcessor(typeof(TPostProcessor), lifetime);

    public OpenMediatRConfiguration AddRequestPostProcessor(Type postProcessorType, ServiceLifetime? lifetime = null)
    {
        if (postProcessorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"{postProcessorType.Name} cannot be an open generic type.", nameof(postProcessorType));

        var serviceTypes = postProcessorType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestPostProcessor<,>))
            .ToList();

        if (serviceTypes.Count == 0)
            throw new ArgumentException(
                $"{postProcessorType.Name} must implement {typeof(IRequestPostProcessor<,>).Name}.", nameof(postProcessorType));

        foreach (var serviceType in serviceTypes)
            RequestPostProcessorsToRegister.Add(
                new ServiceDescriptor(serviceType, postProcessorType, lifetime ?? ServiceLifetime.Transient));

        return this;
    }

    /// <summary>
    /// Explicitly registers an <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>
    /// implementation. The exception-handler wrapper behavior is wired automatically.
    /// </summary>
    public OpenMediatRConfiguration AddRequestExceptionHandler<TExceptionHandler>(ServiceLifetime? lifetime = null)
        where TExceptionHandler : class
        => AddRequestExceptionHandler(typeof(TExceptionHandler), lifetime);

    public OpenMediatRConfiguration AddRequestExceptionHandler(Type exceptionHandlerType, ServiceLifetime? lifetime = null)
    {
        if (exceptionHandlerType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"{exceptionHandlerType.Name} cannot be an open generic type.", nameof(exceptionHandlerType));

        var serviceTypes = exceptionHandlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestExceptionHandler<,,>))
            .ToList();

        if (serviceTypes.Count == 0)
            throw new ArgumentException(
                $"{exceptionHandlerType.Name} must implement {typeof(IRequestExceptionHandler<,,>).Name}.", nameof(exceptionHandlerType));

        foreach (var serviceType in serviceTypes)
            RequestExceptionHandlersToRegister.Add(
                new ServiceDescriptor(serviceType, exceptionHandlerType, lifetime ?? ServiceLifetime.Transient));

        return this;
    }

    /// <summary>
    /// Explicitly registers an <see cref="IRequestExceptionAction{TRequest, TException}"/>
    /// implementation. The exception-action wrapper behavior is wired automatically.
    /// </summary>
    public OpenMediatRConfiguration AddRequestExceptionAction<TExceptionAction>(ServiceLifetime? lifetime = null)
        where TExceptionAction : class
        => AddRequestExceptionAction(typeof(TExceptionAction), lifetime);

    public OpenMediatRConfiguration AddRequestExceptionAction(Type exceptionActionType, ServiceLifetime? lifetime = null)
    {
        if (exceptionActionType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"{exceptionActionType.Name} cannot be an open generic type.", nameof(exceptionActionType));

        var serviceTypes = exceptionActionType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestExceptionAction<,>))
            .ToList();

        if (serviceTypes.Count == 0)
            throw new ArgumentException(
                $"{exceptionActionType.Name} must implement {typeof(IRequestExceptionAction<,>).Name}.", nameof(exceptionActionType));

        foreach (var serviceType in serviceTypes)
            RequestExceptionActionsToRegister.Add(
                new ServiceDescriptor(serviceType, exceptionActionType, lifetime ?? ServiceLifetime.Transient));

        return this;
    }
}
