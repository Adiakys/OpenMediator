using System.Collections.Concurrent;
using System.Reflection;
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
    private readonly ConcurrentDictionary<Type, ClosedHandler[]> _closedHandlers = new();
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
    /// handler that closes to <paramref name="serviceType"/>, otherwise <see langword="null"/>.
    /// </summary>
    internal object? ResolveSingle(IServiceProvider services, Type serviceType)
    {
        var registered = services.GetService(serviceType);
        if (registered is not null)
            return registered;

        var candidates = Close(serviceType);

        return candidates.Length switch
        {
            0 => null,
            1 => candidates[0].Factory(services, null),
            _ => throw new InvalidOperationException(
                $"Request '{serviceType.GetGenericArguments()[0].FullName}' matches more than one generic handler: " +
                $"{string.Join(", ", candidates.Select(c => $"'{c.Implementation.FullName}'"))}. A request has exactly one handler."),
        };
    }

    /// <summary>
    /// The notification types whose handlers receive a notification of <paramref name="notificationType"/>, most specific
    /// first: the type itself, then base classes and interfaces that have scanned or generic handlers.
    /// </summary>
    internal NotificationTarget[] GetNotificationTargets(Type notificationType)
    {
        return _notificationTargets.GetOrAdd(notificationType, type =>
        {
            var baseClasses = new List<Type>();
            for (var current = type.BaseType; current is not null && typeof(INotification).IsAssignableFrom(current); current = current.BaseType)
                baseClasses.Add(current);

            var interfaces = type.GetInterfaces().Where(i => typeof(INotification).IsAssignableFrom(i));

            return new[] { type }
                .Concat(baseClasses)
                .Concat(interfaces)
                .Select(t => (Notification: t, HandlerType: typeof(INotificationHandler<>).MakeGenericType(t)))
                .Select(t => new NotificationTarget(
                    t.HandlerType,
                    typeof(IEnumerable<>).MakeGenericType(t.HandlerType),
                    t.HandlerType.GetMethod("Handle", [t.Notification, typeof(CancellationToken)])!,
                    Close(t.HandlerType)))
                // The notification's own type is always looked up, so handlers registered by hand keep working.
                .Where((target, index) => index == 0 || target.GenericHandlers.Length > 0 || _notificationHandlerTypes.Contains(target.HandlerType))
                .ToArray();
        });
    }

    /// <summary>Every handler of <paramref name="target"/>: the container's registrations, then the generic handlers.</summary>
    internal IEnumerable<object> ResolveAll(IServiceProvider services, NotificationTarget target)
    {
        if (services.GetService(target.EnumerableType) is IEnumerable<object> registered)
        {
            foreach (var handler in registered)
                yield return handler;
        }

        foreach (var generic in target.GenericHandlers)
            yield return generic.Factory(services, null);
    }

    private ClosedHandler[] Close(Type serviceType)
    {
        if (_genericHandlers.Count == 0)
            return [];

        return _closedHandlers.GetOrAdd(serviceType, type => _genericHandlers
            .Where(h => h.ServiceInterface.GetGenericTypeDefinition() == type.GetGenericTypeDefinition())
            .Select(h => TryClose(h, type))
            .OfType<Type>()
            .Select(t => new ClosedHandler(t, ActivatorUtilities.CreateFactory(t, Type.EmptyTypes)))
            .ToArray());
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

    internal sealed record ClosedHandler(Type Implementation, ObjectFactory Factory);

    internal sealed record NotificationTarget(Type HandlerType, Type EnumerableType, MethodInfo HandleMethod, ClosedHandler[] GenericHandlers);
}
