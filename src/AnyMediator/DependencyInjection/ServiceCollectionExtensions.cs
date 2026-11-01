using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AnyMediator.NotificationSinks;
using AnyMediator.RequestProcessors;

namespace AnyMediator;

/// <summary>
/// Registers AnyMediator in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISender"/>, <see cref="IPublisher"/>, <see cref="IMediator"/> and the handlers found in the
    /// configured assemblies. The AnyMediator equivalent of MediatR's <c>AddMediatR</c>; calling it more than once
    /// does not duplicate registrations.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddAnyMediator(cfg =>
    /// {
    ///     cfg.RegisterServicesFromAssemblyContaining&lt;Program&gt;();
    ///     cfg.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;));
    /// });
    /// </code>
    /// </example>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures assemblies to scan, behaviors, processors and lifetimes.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAnyMediator(
        this IServiceCollection services,
        Action<AnyMediatorConfiguration> configure)
    {
        var config = new AnyMediatorConfiguration();
        configure(config);

        var scannedTypes = ScanTypes(config);
        var closedTypes = scannedTypes.Where(t => !t.IsGenericTypeDefinition).ToArray();

        RegisterHandlers(services, HandlerRegistry.GetOrAdd(services), scannedTypes);

        // The pipeline runs behaviors in registration order: the first registered is the outermost.
        // Exception handler/action wrappers go first so they catch exceptions thrown anywhere in the
        // pipeline; user behaviors follow in the order they were added; pre/post-processor wrappers go
        // last so they end up innermost (closest to the handler).
        // Exception handlers and actions are always scanned, as in MediatR; processors only when opted in.
        var hasExceptionHandler = RegisterScanned(services, closedTypes, typeof(IRequestExceptionHandler<,,>), config.RequestExceptionHandlersToRegister);
        var hasExceptionAction = RegisterScanned(services, closedTypes, typeof(IRequestExceptionAction<,>), config.RequestExceptionActionsToRegister);

        // By default the action wrapper is outer: a handler that recovers via SetHandled hides the exception from the
        // actions. With ApplyForAllExceptions the handler wrapper is outer, so actions always see the exception first.
        var handlerWrapper = (typeof(RequestExceptionProcessorBehavior<,>), hasExceptionHandler);
        var actionWrapper = (typeof(RequestExceptionActionProcessorBehavior<,>), hasExceptionAction);
        AddWrappers(services, config.Lifetime,
            config.RequestExceptionActionProcessorStrategy == RequestExceptionActionProcessorStrategy.ApplyForAllExceptions
                ? [handlerWrapper, actionWrapper]
                : [actionWrapper, handlerWrapper]);

        services.TryAddEnumerable(config.BehaviorsToRegister);
        services.TryAddEnumerable(config.StreamBehaviorsToRegister);

        var processorTypes = config.AutoRegisterRequestProcessors ? closedTypes : [];
        var hasPreProcessor = RegisterScanned(services, processorTypes, typeof(IRequestPreProcessor<>), config.RequestPreProcessorsToRegister);
        var hasPostProcessor = RegisterScanned(services, processorTypes, typeof(IRequestPostProcessor<,>), config.RequestPostProcessorsToRegister);
        AddWrappers(services, config.Lifetime,
            [(typeof(RequestPostProcessorBehavior<,>), hasPostProcessor), (typeof(RequestPreProcessorBehavior<,>), hasPreProcessor)]);

        var lifetime = config.Lifetime;
        services.TryAdd(new ServiceDescriptor(typeof(ISender), typeof(AnyMediatorSender), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), typeof(AnyMediatorPublisher), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), lifetime));

        // Instance overrides Type if both are set; otherwise fall back to the configured Type. The built-in strategies
        // hold no state, so one instance serves every Publish.
        if (config.NotificationPublisher is not null)
            services.TryAddSingleton(config.NotificationPublisher);
        else
            services.TryAdd(new ServiceDescriptor(typeof(INotificationPublisher), config.NotificationPublisherType,
                config.NotificationPublisherType.Assembly == typeof(AnyMediatorPublisher).Assembly ? ServiceLifetime.Singleton : lifetime));

        services.TryAddEnumerable(ServiceDescriptor.Describe(
            typeof(INotificationSink), typeof(InMemoryNotificationSink), lifetime));

        return services;
    }

    private static Type[] ScanTypes(AnyMediatorConfiguration config)
    {
        return config.AssembliesToRegister
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && config.TypeEvaluator(t))
            .ToArray();
    }

    private static void RegisterHandlers(IServiceCollection services, HandlerRegistry registry, Type[] scannedTypes)
    {
        foreach (var type in scannedTypes)
        {
            foreach (var iface in type.GetInterfaces())
            {
                var def = iface.IsGenericType ? iface.GetGenericTypeDefinition() : null;

                if (def != typeof(IRequestHandler<,>) && def != typeof(IStreamRequestHandler<,>) && def != typeof(INotificationHandler<>))
                    continue;

                // Open generic handlers are closed when a message is dispatched (see HandlerRegistry). Registered as
                // themselves, so the container can create AuditHandler<Order> from AuditHandler<> and dispose it.
                if (type.IsGenericTypeDefinition)
                {
                    registry.AddGenericHandler(type, iface);
                    services.TryAddTransient(type);
                }
                else if (def == typeof(INotificationHandler<>))
                {
                    registry.AddNotificationHandler(iface);
                    services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
                }
                else
                {
                    registry.AddSingleHandler(iface, type);
                    services.TryAddTransient(iface, type);
                }
            }
        }
    }

    /// <summary>
    /// Registers every scanned implementation of <paramref name="openInterface"/>, then the explicit registrations.
    /// Returns whether any exist, which decides if the matching wrapper behavior is needed.
    /// </summary>
    private static bool RegisterScanned(
        IServiceCollection services, Type[] types, Type openInterface, List<ServiceDescriptor> explicitDescriptors)
    {
        var found = false;
        foreach (var type in types)
        {
            foreach (var iface in type.GetClosedInterfaces(openInterface))
            {
                services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
                found = true;
            }
        }

        services.TryAddEnumerable(explicitDescriptors);
        return found || explicitDescriptors.Count > 0;
    }

    private static void AddWrappers(IServiceCollection services, ServiceLifetime lifetime, (Type Behavior, bool Needed)[] wrappers)
    {
        foreach (var (behavior, needed) in wrappers)
        {
            if (needed)
                services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(IPipelineBehavior<,>), behavior, lifetime));
        }
    }
}
