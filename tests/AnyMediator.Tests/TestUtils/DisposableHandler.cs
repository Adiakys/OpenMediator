namespace AnyMediator.Tests;

internal sealed class DisposableHandler : IRequestHandler<DisposableRequest, string>, IDisposable
{
    public Task<string> Handle(DisposableRequest request, CancellationToken cancellationToken)
        => Task.FromResult("disposable");

    public void Dispose() { }
}
