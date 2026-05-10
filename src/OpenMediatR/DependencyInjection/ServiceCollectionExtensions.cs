using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenMediatR.NotificationSinks;

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

        RegisterHandlersFromAssemblies(services, config);

        foreach (var descriptor in config.BehaviorsToRegister)
            services.Add(descriptor);

        var lifetime = config.Lifetime;
        services.TryAdd(new ServiceDescriptor(typeof(ISender), typeof(OpenMediatRSender), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), typeof(OpenMediatRPublisher), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(INotificationPublisher), config.NotificationPublisherType, lifetime));
        services.TryAddEnumerable(ServiceDescriptor.Describe(
            typeof(INotificationSink), typeof(InMemoryNotificationSink), lifetime));

        return services;
    }

    private static void RegisterHandlersFromAssemblies(
        IServiceCollection services, OpenMediatRConfiguration config)
    {
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
                    services.AddTransient(iface, type);
            }
        }
    }
}
