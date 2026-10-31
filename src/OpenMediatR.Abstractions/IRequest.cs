namespace OpenMediatR;

/// <summary>
/// Marker for a request that returns a <typeparamref name="TResponse"/>. Sent with
/// <see cref="ISender.Send{TResponse}"/> and handled by exactly one
/// <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequest<out TResponse>
{
}

/// <summary>
/// Marker for a request that returns no value (a command). Handled by an <see cref="IRequestHandler{TRequest}"/>;
/// internally its response type is <see cref="Unit"/>.
/// </summary>
public interface IRequest : IRequest<Unit>
{
}
