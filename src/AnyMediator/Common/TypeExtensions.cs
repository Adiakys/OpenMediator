namespace AnyMediator;

internal static class TypeExtensions
{
    internal static IEnumerable<object> GetServicesOrDefault(this IServiceProvider services, Type serviceType)
    {
        var genericEnumerable = typeof(IEnumerable<>).MakeGenericType(serviceType);
        return (IEnumerable<object>?)services.GetService(genericEnumerable) ?? [];
    }
}