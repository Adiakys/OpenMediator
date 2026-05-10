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

        if (config.AssembliesToRegister.Count == 0)
            throw new ArgumentException(
                "No assemblies found to scan. Supply at least one assembly to scan for handlers.");

        var (hasPreProcessor, hasPostProcessor) = RegisterHandlersFromAssemblies(services, config);

        var lifetime = config.Lifetime;

        // Pre/post-processor wrappers are registered before user behaviors so they end up innermost
        // (closest to the handler). Pipeline construction wraps later registrations around earlier ones,
        // so user behaviors register after and become outer wrappers — matching MediatR's semantics.
        if (hasPreProcessor)
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>), lifetime));

        if (hasPostProcessor)
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>), lifetime));

        foreach (var descriptor in config.BehaviorsToRegister)
            services.Add(descriptor);

        services.TryAdd(new ServiceDescriptor(typeof(ISender), typeof(OpenMediatRSender), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), typeof(OpenMediatRPublisher), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(INotificationPublisher), config.NotificationPublisherType, lifetime));
        services.TryAddEnumerable(ServiceDescriptor.Describe(
            typeof(INotificationSink), typeof(InMemoryNotificationSink), lifetime));

        return services;
    }

    private static (bool HasPreProcessor, bool HasPostProcessor) RegisterHandlersFromAssemblies(
        IServiceCollection services, OpenMediatRConfiguration config)
    {
        var hasPreProcessor = false;
        var hasPostProcessor = false;

        var types = config.AssembliesToRegister
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition);

        foreach (var type in types)
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
                else if (config.AutoRegisterRequestProcessors && def == typeof(IRequestPreProcessor<>))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
                    hasPreProcessor = true;
                }
                else if (config.AutoRegisterRequestProcessors && def == typeof(IRequestPostProcessor<,>))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
                    hasPostProcessor = true;
                }
            }
        }

        return (hasPreProcessor, hasPostProcessor);
    }
}
