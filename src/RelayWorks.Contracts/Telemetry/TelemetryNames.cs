namespace RelayWorks.Contracts.Telemetry;

public static class TelemetryNames
{
    public const string ControlPlaneSource = "RelayWorks.ControlPlane";
    public const string WorkerSource = "RelayWorks.Sync.Worker";
    public const string ControlPlaneMeter = "RelayWorks.ControlPlane.Metrics";
    public const string WorkerMeter = "RelayWorks.Sync.Worker.Metrics";
    public const string SecretMeter = "RelayWorks.Sync.Worker.Secrets";
    public const string TraceParentProperty = "traceparent";
    public const string TraceStateProperty = "tracestate";
}
