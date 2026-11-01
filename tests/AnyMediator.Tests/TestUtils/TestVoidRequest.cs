namespace AnyMediator.Tests;

internal sealed record TestVoidRequest(Action? Callback = null) : IRequest;

internal sealed class TestVoidHandler : IRequestHandler<TestVoidRequest>
{
    public int Count { get; private set; }

    public Task Handle(TestVoidRequest request, CancellationToken cancellationToken)
    {
        Count++;
        request.Callback?.Invoke();
        return Task.CompletedTask;
    }
}
