namespace AnyMediator;

/// <summary>
/// Combines <see cref="ISender"/> and <see cref="IPublisher"/> in a single entry point.
/// Prefer injecting the narrower <see cref="ISender"/> or <see cref="IPublisher"/> when only one is needed.
/// </summary>
public interface IMediator : ISender, IPublisher
{
}
