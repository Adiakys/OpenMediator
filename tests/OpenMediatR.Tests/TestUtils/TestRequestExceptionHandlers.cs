using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR.Tests;

/// <summary>
/// Recovers TestRequest from <see cref="InvalidOperationException"/> with a fallback response.
/// Trace is optional — read from DI so this fixture is benign when scanned by tests that
/// don't supply a List&lt;string&gt; service.
/// </summary>
internal sealed class TestExceptionHandler : IRequestExceptionHandler<TestRequest, string, InvalidOperationException>
{
    private readonly IServiceProvider _services;
    public TestExceptionHandler(IServiceProvider services) => _services = services;

    public Task Handle(
        TestRequest request,
        InvalidOperationException exception,
        RequestExceptionHandlerState<string> state,
        CancellationToken cancellationToken)
    {
        _services.GetService<List<string>>()?.Add($"{nameof(TestExceptionHandler)}:{exception.Message}");

        if (_services.GetService<TestExceptionRecoveryResponse>() is { } recovery)
            state.SetHandled(recovery.Value);

        return Task.CompletedTask;
    }
}

internal sealed class TestExceptionAction : IRequestExceptionAction<TestRequest, Exception>
{
    private readonly IServiceProvider _services;
    public TestExceptionAction(IServiceProvider services) => _services = services;

    public Task Execute(TestRequest request, Exception exception, CancellationToken cancellationToken)
    {
        _services.GetService<List<string>>()?.Add($"{nameof(TestExceptionAction)}:{exception.GetType().Name}");
        return Task.CompletedTask;
    }
}

internal sealed record TestExceptionRecoveryResponse(string Value);
