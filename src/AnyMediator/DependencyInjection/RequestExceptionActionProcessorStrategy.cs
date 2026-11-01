namespace AnyMediator;

/// <summary>
/// Determines when <see cref="IRequestExceptionAction{TRequest, TException}"/> implementations fire
/// relative to <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>.
/// </summary>
public enum RequestExceptionActionProcessorStrategy
{
    /// <summary>
    /// Default. Actions run only when no exception handler recovered via
    /// <see cref="RequestExceptionHandlerState{TResponse}.SetHandled"/>. If a handler recovers, the
    /// exception is suppressed before the action wrapper sees it and no action runs.
    /// </summary>
    ApplyForUnhandledExceptions,

    /// <summary>
    /// Actions always run when an exception is thrown — even if a downstream handler later recovers
    /// it. Useful for logging/metrics that must fire on every failure regardless of recovery.
    /// </summary>
    ApplyForAllExceptions,
}
