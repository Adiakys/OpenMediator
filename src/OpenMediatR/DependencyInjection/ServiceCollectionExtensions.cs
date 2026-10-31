using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenMediatR.NotificationSinks;
using OpenMediatR.RequestProcessors;

namespace OpenMediatR;

/// <summary>
/// Registers OpenMediatR in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISender"/>, <see cref="IPublisher"/>, <see cref="IMediator"/> and the handlers found in the
    /// configured assemblies. The OpenMediatR equivalent of MediatR's <c>AddMediatR</c>; calling it more than once
    /// does not duplicate registrations.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddOpenMediatR(cfg =>
    /// {
    ///     cfg.RegisterServicesFromAssemblyContaining&lt;Program&gt;();
    ///     cfg.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;));
    /// });
    /// </code>
    /// </example>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures assemblies to scan, behaviors, processors and lifetimes.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOpenMediatR(
        this IServiceCollection services,
        Action<OpenMediatRConfiguration> configure)
    {
        var config = new OpenMediatRConfiguration();
        configure(config);

        var candidateTypes = ScanCandidateTypes(config);

        RegisterHandlers(services, candidateTypes);

        // The pipeline runs behaviors in registration order: the first registered is the outermost.
        // Exception handler/action wrappers go first so they catch exceptions thrown anywhere in the
        // pipeline; user behaviors follow in the order they were added; pre/post-processor wrappers go
        // last so they end up innermost (closest to the handler).
        ConfigureRequestExceptionHandlers(services, config, candidateTypes);

        foreach (var descriptor in config.BehaviorsToRegister)
            services.TryAddEnumerable(descriptor);

        foreach (var descriptor in config.StreamBehaviorsToRegister)
            services.TryAddEnumerable(descriptor);

        ConfigureRequestProcessors(services, config, candidateTypes);

        var lifetime = config.Lifetime;
        services.TryAdd(new ServiceDescriptor(typeof(ISender), typeof(OpenMediatRSender), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), typeof(OpenMediatRPublisher), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), lifetime));

        // Instance overrides Type if both are set; otherwise fall back to the configured Type.
        if (config.NotificationPublisher is not null)
            services.TryAddSingleton(config.NotificationPublisher);
        else
            services.TryAdd(new ServiceDescriptor(typeof(INotificationPublisher), config.NotificationPublisherType, lifetime));

        services.TryAddEnumerable(ServiceDescriptor.Describe(
            typeof(INotificationSink), typeof(InMemoryNotificationSink), lifetime));

        return services;
    }

    private static IReadOnlyList<Type> ScanCandidateTypes(OpenMediatRConfiguration config)
    {
        return config.AssembliesToRegister
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
            .ToArray();
    }

    private static void RegisterHandlers(IServiceCollection services, IReadOnlyList<Type> candidateTypes)
    {
        foreach (var type in candidateTypes)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                var def = iface.GetGenericTypeDefinition();

                if (def == typeof(IRequestHandler<,>))
                    services.TryAddTransient(iface, type);
                else if (def == typeof(IStreamRequestHandler<,>))
                    services.TryAddTransient(iface, type);
                else if (def == typeof(INotificationHandler<>))
                    services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
            }
        }
    }

    private static void ConfigureRequestProcessors(
        IServiceCollection services, OpenMediatRConfiguration config, IReadOnlyList<Type> candidateTypes)
    {
        var (hasPreFromScan, hasPostFromScan) = config.AutoRegisterRequestProcessors
            ? ScanRequestProcessors(services, candidateTypes)
            : (false, false);

        foreach (var descriptor in config.RequestPreProcessorsToRegister)
            services.TryAddEnumerable(descriptor);

        foreach (var descriptor in config.RequestPostProcessorsToRegister)
            services.TryAddEnumerable(descriptor);

        var hasPreProcessor = hasPreFromScan || config.RequestPreProcessorsToRegister.Count > 0;
        var hasPostProcessor = hasPostFromScan || config.RequestPostProcessorsToRegister.Count > 0;

        if (hasPostProcessor)
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>), config.Lifetime));

        if (hasPreProcessor)
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>), config.Lifetime));
    }

    private static (bool HasPreProcessor, bool HasPostProcessor) ScanRequestProcessors(
        IServiceCollection services, IReadOnlyList<Type> candidateTypes)
    {
        var hasPreProcessor = false;
        var hasPostProcessor = false;

        foreach (var type in candidateTypes)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                var def = iface.GetGenericTypeDefinition();

                if (def == typeof(IRequestPreProcessor<>))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
                    hasPreProcessor = true;
                }
                else if (def == typeof(IRequestPostProcessor<,>))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
                    hasPostProcessor = true;
                }
            }
        }

        return (hasPreProcessor, hasPostProcessor);
    }

    private static void ConfigureRequestExceptionHandlers(
        IServiceCollection services, OpenMediatRConfiguration config, IReadOnlyList<Type> candidateTypes)
    {
        // Auto-scan is unconditional, mirroring MediatR (no opt-in flag for exception handlers/actions).
        var (hasHandlerFromScan, hasActionFromScan) = ScanRequestExceptionHandlers(services, candidateTypes);

        foreach (var descriptor in config.RequestExceptionHandlersToRegister)
            services.TryAddEnumerable(descriptor);

        foreach (var descriptor in config.RequestExceptionActionsToRegister)
            services.TryAddEnumerable(descriptor);

        var hasHandler = hasHandlerFromScan || config.RequestExceptionHandlersToRegister.Count > 0;
        var hasAction = hasActionFromScan || config.RequestExceptionActionsToRegister.Count > 0;

        // Wrapper order is determined by RequestExceptionActionProcessorStrategy (first registered = outer):
        //  - ApplyForUnhandledExceptions (default): handler wrapper inner, action wrapper outer.
        //    Recovery via SetHandled suppresses the exception before it reaches the action wrapper.
        //  - ApplyForAllExceptions: action wrapper inner, handler wrapper outer.
        //    Action always sees the exception first; handler may then recover.
        if (config.RequestExceptionActionProcessorStrategy == RequestExceptionActionProcessorStrategy.ApplyForAllExceptions)
        {
            if (hasHandler)
                services.TryAddEnumerable(ServiceDescriptor.Describe(
                    typeof(IPipelineBehavior<,>), typeof(RequestExceptionProcessorBehavior<,>), config.Lifetime));

            if (hasAction)
                services.TryAddEnumerable(ServiceDescriptor.Describe(
                    typeof(IPipelineBehavior<,>), typeof(RequestExceptionActionProcessorBehavior<,>), config.Lifetime));
        }
        else
        {
            if (hasAction)
                services.TryAddEnumerable(ServiceDescriptor.Describe(
                    typeof(IPipelineBehavior<,>), typeof(RequestExceptionActionProcessorBehavior<,>), config.Lifetime));

            if (hasHandler)
                services.TryAddEnumerable(ServiceDescriptor.Describe(
                    typeof(IPipelineBehavior<,>), typeof(RequestExceptionProcessorBehavior<,>), config.Lifetime));
        }
    }

    private static (bool HasHandler, bool HasAction) ScanRequestExceptionHandlers(
        IServiceCollection services, IReadOnlyList<Type> candidateTypes)
    {
        var hasHandler = false;
        var hasAction = false;

        foreach (var type in candidateTypes)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                var def = iface.GetGenericTypeDefinition();

                if (def == typeof(IRequestExceptionHandler<,,>))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
                    hasHandler = true;
                }
                else if (def == typeof(IRequestExceptionAction<,>))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
                    hasAction = true;
                }
            }
        }

        return (hasHandler, hasAction);
    }
}
