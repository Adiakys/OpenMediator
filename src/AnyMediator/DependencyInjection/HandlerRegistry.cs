using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator;

/// <summary>
/// Where the sender and the in-memory sink find handlers. It knows what assembly scanning found and the container
/// cannot hold on its own: which class handles each request type (to report duplicates across repeated
/// <c>AddAnyMediator</c> calls), which notification types have scanned handlers, and the open generic handlers, whose
/// service interface is only closed when a message is dispatched. One instance per service collection.
/// </summary>
internal sealed class HandlerRegistry
{
    private readonly Dictionary<Type, Type> _singleHandlers = [];
    private readonly HashSet<Type> _notificationHandlerTypes = [];
    private readonly List<GenericHandler> _genericHandlers = [];
    private readonly ConcurrentDictionary<Type, Type[]> _closedHandlers = new();
    private readonly ConcurrentDictionary<Type, NotificationTarget[]> _notificationTargets = new();

    internal static HandlerRegistry GetOrAdd(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(HandlerRegistry))?.ImplementationInstance is HandlerRegistry existing)
            return existing;

        var registry = new HandlerRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    /// <summary>
    /// Records <paramref name="implementationType"/> as the handler of a request or stream request; throws when another
    /// class already handles the same request.
    /// </summary>
    internal void AddSingleHandler(Type serviceType, Type implementationType)
    {
        if (_singleHandlers.TryAdd(serviceType, implementationType) || _singleHandlers[serviceType] == implementationType)
            return;

        throw new InvalidOperationException(
            $"Request '{serviceType.GetGenericArguments()[0].FullName}' has more than one handler: " +
            $"'{_singleHandlers[serviceType].FullName}' and '{implementationType.FullName}'. " +
            "A request has exactly one handler: remove one of them, or leave it out of scanning with cfg.TypeEvaluator.");
    }

    internal void AddNotificationHandler(Type serviceType) => _notificationHandlerTypes.Add(serviceType);

    internal void AddGenericHandler(Type implementationType, Type serviceInterface)
    {
        if (!_genericHandlers.Any(h => h.Implementation == implementationType && h.ServiceInterface == serviceInterface))
            _genericHandlers.Add(new GenericHandler(implementationType, serviceInterface));
    }

    /// <summary>
    /// The handler of a request or stream request: the container's registration, otherwise the single open generic
    /// handler that closes to <typeparamref name="THandler"/>.
    /// </summary>
    internal THandler GetRequired<THandler>(IServiceProvider services)
    {
        if (services.GetService(typeof(THandler)) is THandler registered)
            return registered;

        var handlerType = typeof(THandler);
        var requestType = handlerType.GetGenericArguments()[0];
        var candidates = Close(handlerType);

        return candidates.Length switch
        {
            1 => (THandler)CreateGeneric(services, candidates[0]),
            // Spell out the fix: callers (and coding agents) act on the message without reading the source.
            0 => throw new InvalidOperationException(
                $"No handler registered for request '{requestType.FullName}'. Expected a non-abstract class implementing " +
                $"{handlerType.GetGenericTypeDefinition().Name.Split('`')[0]}<{string.Join(", ", handlerType.GetGenericArguments().Select(t => t.Name))}> " +
                "in an assembly scanned by AddAnyMediator: add cfg.RegisterServicesFromAssemblyContaining<T>() with T declared in the handler's assembly."),
            _ => throw new InvalidOperationException(
                $"Request '{requestType.FullName}' matches more than one generic handler: " +
                $"{string.Join(", ", candidates.Select(c => $"'{c.FullName}'"))}. A request has exactly one handler."),
        };
    }

    /// <summary>
    /// The notification types whose handlers receive a notification of <paramref name="notificationType"/>, most specific
    /// first: the type itself, then base classes and interfaces that have scanned or generic handlers.
    /// </summary>
    internal NotificationTarget[] GetNotificationTargets(Type notificationType)
        => _notificationTargets.GetOrAdd(notificationType, static (type, registry) => registry.CreateNotificationTargets(type), this);

    private NotificationTarget[] CreateNotificationTargets(Type notificationType)
    {
        var types = new List<Type>();
        for (var type = notificationType; type is not null && typeof(INotification).IsAssignableFrom(type); type = type.BaseType)
            types.Add(type);
        types.AddRange(notificationType.GetInterfaces().Where(i => typeof(INotification).IsAssignableFrom(i)));

        var targets = new List<NotificationTarget>();
        foreach (var type in types)
        {
            var handlerType = typeof(INotificationHandler<>).MakeGenericType(type);
            var genericHandlers = Close(handlerType);

            // The notification's own type is always looked up, so handlers registered by hand keep working.
            if (type == notificationType || genericHandlers.Length > 0 || _notificationHandlerTypes.Contains(handlerType))
                targets.Add((NotificationTarget)Activator.CreateInstance(typeof(NotificationTarget<>).MakeGenericType(type), [genericHandlers])!);
        }

        return [.. targets];
    }

    /// <summary>
    /// An instance of a closed generic handler. <c>AddAnyMediator</c> registers each open generic handler as itself, so
    /// the container creates it and disposes it with its scope; a provider without that registration, such as a test
    /// double, gets an untracked instance.
    /// </summary>
    internal static object CreateGeneric(IServiceProvider services, Type handlerType)
        => ActivatorUtilities.GetServiceOrCreateInstance(services, handlerType);

    private Type[] Close(Type serviceType)
    {
        if (_genericHandlers.Count == 0)
            return [];

        return _closedHandlers.GetOrAdd(serviceType, static (type, genericHandlers) => genericHandlers
            .Where(h => h.ServiceInterface.GetGenericTypeDefinition() == type.GetGenericTypeDefinition())
            .Select(h => TryClose(h, type))
            .OfType<Type>()
            .ToArray(), _genericHandlers);
    }

    private static Type? TryClose(GenericHandler handler, Type serviceType)
    {
        var bindings = new Dictionary<Type, Type>();
        if (!TryBind(handler.ServiceInterface, serviceType, bindings))
            return null;

        // Every type parameter of the handler must appear in the interface, otherwise it cannot be inferred.
        var parameters = handler.Implementation.GetGenericArguments();
        var arguments = new Type[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!bindings.TryGetValue(parameters[i], out var argument))
                return null;
            arguments[i] = argument;
        }

        try
        {
            return handler.Implementation.MakeGenericType(arguments);
        }
        catch (ArgumentException)
        {
            return null; // a generic constraint of the handler is not satisfied
        }
    }

    /// <summary>
    /// Matches a type that contains the handler's type parameters (for example <c>EntityCreated&lt;T&gt;</c>) against a
    /// concrete type (<c>EntityCreated&lt;Order&gt;</c>), recording what each parameter stands for.
    /// </summary>
    private static bool TryBind(Type pattern, Type concrete, Dictionary<Type, Type> bindings)
    {
        if (pattern.IsGenericParameter)
        {
            if (bindings.TryGetValue(pattern, out var bound))
                return bound == concrete;

            bindings[pattern] = concrete;
            return true;
        }

        if (!pattern.ContainsGenericParameters)
            return pattern == concrete;

        if (pattern.IsArray)
            return concrete.IsArray
                && pattern.GetArrayRank() == concrete.GetArrayRank()
                && TryBind(pattern.GetElementType()!, concrete.GetElementType()!, bindings);

        if (!pattern.IsGenericType || !concrete.IsGenericType
            || pattern.GetGenericTypeDefinition() != concrete.GetGenericTypeDefinition())
            return false;

        var patternArguments = pattern.GetGenericArguments();
        var concreteArguments = concrete.GetGenericArguments();
        for (var i = 0; i < patternArguments.Length; i++)
        {
            if (!TryBind(patternArguments[i], concreteArguments[i], bindings))
                return false;
        }

        return true;
    }

    private sealed record GenericHandler(Type Implementation, Type ServiceInterface);
}
