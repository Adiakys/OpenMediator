namespace AnyMediator.Tests;

internal class TestHandler : IRequestHandler<TestRequest, string>,
    IRequestHandler<TestRequest2, bool>,
    INotificationHandler<TestNotification>
{
    public int RequestCount { get; private set; } = 0;
    public int NotificationCount { get; private set; } = 0;

    public virtual Task<string> Handle(TestRequest request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return Task.FromResult("Test");
    }

    public virtual Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        NotificationCount++;
        return Task.CompletedTask;
    }

    public Task<bool> Handle(TestRequest2 request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.Input);
    }
}