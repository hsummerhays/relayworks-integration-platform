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
    public static readonly Counter<long> ArchiveCandidates = Meter.CreateCounter<long>("relayworks.archive.candidates");
    public static readonly Counter<long> RunsArchived = Meter.CreateCounter<long>("relayworks.archive.runs");
    public static readonly Counter<long> RecordsArchived = Meter.CreateCounter<long>("relayworks.archive.records");
    public static readonly Counter<long> ArchiveFailures = Meter.CreateCounter<long>("relayworks.archive.failures");
    public static readonly Counter<long> RetentionRowsDeleted = Meter.CreateCounter<long>("relayworks.retention.rows.deleted");
}
