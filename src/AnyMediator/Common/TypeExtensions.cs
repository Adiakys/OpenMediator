namespace AnyMediator;

internal static class TypeExtensions
{
    /// <summary>
    /// All registrations of <typeparamref name="T"/> as an array; empty when the provider returns nothing, as a test
    /// double may. The Microsoft container already returns an array, which is used as is.
    /// </summary>
    internal static T[] GetAll<T>(this IServiceProvider services) => services.GetService(typeof(IEnumerable<T>)) switch
    {
        T[] array => array,
        IEnumerable<T> sequence => sequence.ToArray(),
        _ => [],
    };

    /// <summary>All registrations of <paramref name="serviceType"/>, for types known only at runtime.</summary>
    internal static object[] GetAll(this IServiceProvider services, Type serviceType)
        => services.GetService(typeof(IEnumerable<>).MakeGenericType(serviceType)) as object[] ?? [];

    /// <summary>The closed forms of <paramref name="openInterface"/> that <paramref name="type"/> implements.</summary>
    internal static Type[] GetClosedInterfaces(this Type type, Type openInterface)
        => type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface).ToArray();
}
