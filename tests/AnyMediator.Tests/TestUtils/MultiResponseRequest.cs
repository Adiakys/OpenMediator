using System.Runtime.CompilerServices;

namespace AnyMediator.Tests;

public abstract record MultiResponseRequestBase : IRequest<string>;

public sealed record MultiResponseRequest : MultiResponseRequestBase, IRequest<int>;

internal sealed class MultiResponseHandler :
    IRequestHandler<MultiResponseRequest, int>,
    IRequestHandler<MultiResponseRequest, string>
{
    public Task<int> Handle(MultiResponseRequest request, CancellationToken cancellationToken)
        => Task.FromResult(42);

    Task<string> IRequestHandler<MultiResponseRequest, string>.Handle(MultiResponseRequest request, CancellationToken cancellationToken)
        => Task.FromResult("base");
}

public abstract record MultiResponseStreamRequestBase : IStreamRequest<string>;

public sealed record MultiResponseStreamRequest(int Count) : MultiResponseStreamRequestBase, IStreamRequest<int>;

internal sealed class MultiResponseStreamHandler :
    IStreamRequestHandler<MultiResponseStreamRequest, int>,
    IStreamRequestHandler<MultiResponseStreamRequest, string>
{
    public async IAsyncEnumerable<int> Handle(MultiResponseStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }

    async IAsyncEnumerable<string> IStreamRequestHandler<MultiResponseStreamRequest, string>.Handle(MultiResponseStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return $"base{i}";
            await Task.Yield();
        }
    }
}
