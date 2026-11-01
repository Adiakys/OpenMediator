namespace AnyMediator;

/// <summary>
/// Names to subscribe to AnyMediator's traces and metrics, for example with OpenTelemetry:
/// <c>.WithTracing(t => t.AddSource(AnyMediatorDiagnostics.ActivitySourceName))</c> and
/// <c>.WithMetrics(m => m.AddMeter(AnyMediatorDiagnostics.MeterName))</c>. Nothing is recorded until a listener subscribes.
/// </summary>
public static class AnyMediatorDiagnostics
{
    /// <summary>
    /// The <see cref="System.Diagnostics.ActivitySource"/> name. It emits one span per <c>Send</c>, <c>CreateStream</c>
    /// and <c>Publish</c>, and one child span per notification handler.
    /// </summary>
    public const string ActivitySourceName = "AnyMediator";

    /// <summary>
    /// The <see cref="System.Diagnostics.Metrics.Meter"/> name. It records the <c>anymediator.operation.duration</c>
    /// histogram, in seconds.
    /// </summary>
    public const string MeterName = "AnyMediator";
}
