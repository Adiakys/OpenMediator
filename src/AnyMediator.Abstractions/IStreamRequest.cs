namespace AnyMediator;

/// <summary>
/// Marker for a request whose handler streams responses as an <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
public interface IStreamRequest<out TResponse>
{
}
