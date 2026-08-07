using System.Diagnostics;
using System.Diagnostics.Metrics;
using RelayWorks.Contracts.Telemetry;

namespace RelayWorks.Infrastructure.Telemetry;

public static class ControlPlaneTelemetry
{
    public static readonly ActivitySource ActivitySource = new(TelemetryNames.ControlPlaneSource);
    private static readonly Meter Meter = new(TelemetryNames.ControlPlaneMeter);
    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("relayworks.outbox.published");
    public static readonly Counter<long> OutboxFailures = Meter.CreateCounter<long>("relayworks.outbox.failures");
    public static readonly Histogram<double> OutboxLag = Meter.CreateHistogram<double>("relayworks.outbox.lag", "s");
    public static readonly Counter<long> EventsProjected = Meter.CreateCounter<long>("relayworks.events.projected");
}
