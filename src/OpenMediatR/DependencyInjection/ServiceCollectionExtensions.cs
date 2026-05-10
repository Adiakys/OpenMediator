using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenMediatR.NotificationSinks;
using OpenMediatR.RequestProcessors;

namespace OpenMediatR;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenMediatR(
        this IServiceCollection services,
        Action<OpenMediatRConfiguration> configure)
    {
        var config = new OpenMediatRConfiguration();
        configure(config);

        var candidateTypes = ScanCandidateTypes(config);

        RegisterHandlers(services, candidateTypes);

        // Pre/post-processor wrappers are registered before user behaviors so they end up innermost
        // (closest to the handler). Pipeline construction wraps later registrations around earlier ones,
        // so user behaviors register after and become outer wrappers — matching MediatR's semantics.
        ConfigureRequestProcessors(services, config, candidateTypes);

        foreach (var descriptor in config.BehaviorsToRegister)
            services.TryAddEnumerable(descriptor);

        // Exception handler/action wrappers are registered AFTER user behaviors so they become outermost
        // and can catch exceptions thrown anywhere in the pipeline. Action wrapper is registered last,
        // making it the outermost — it runs through the handler wrapper first (which may recover) and
        // only fires its actions when the exception still bubbles up.
        ConfigureRequestExceptionHandlers(services, config, candidateTypes);

        var lifetime = config.Lifetime;
        services.TryAdd(new ServiceDescriptor(typeof(ISender), typeof(OpenMediatRSender), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), typeof(OpenMediatRPublisher), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), lifetime));
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

        if (hasPreProcessor)
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>), config.Lifetime));

        if (hasPostProcessor)
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>), config.Lifetime));
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

        // Handler wrapper registered first → inner. Action wrapper registered after → outermost.
        // Recovery via SetHandled returns a response from the handler wrapper, suppressing the
        // exception before it reaches the action wrapper (so actions don't fire on handled errors).
        if (hasHandler)
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IPipelineBehavior<,>), typeof(RequestExceptionProcessorBehavior<,>), config.Lifetime));

        if (hasAction)
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IPipelineBehavior<,>), typeof(RequestExceptionActionProcessorBehavior<,>), config.Lifetime));
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
