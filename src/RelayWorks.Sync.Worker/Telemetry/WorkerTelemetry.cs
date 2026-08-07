using System.Diagnostics;
using System.Diagnostics.Metrics;
using RelayWorks.Contracts.Telemetry;

namespace RelayWorks.Sync.Worker.Telemetry;

public static class WorkerTelemetry
{
    public static readonly ActivitySource ActivitySource = new(TelemetryNames.WorkerSource);
    private static readonly Meter Meter = new(TelemetryNames.WorkerMeter);
    public static readonly Counter<long> CommandsProcessed = Meter.CreateCounter<long>("relayworks.commands.processed");
    public static readonly Counter<long> RecordsDelivered = Meter.CreateCounter<long>("relayworks.records.delivered");
    public static readonly Counter<long> RecordsAttention = Meter.CreateCounter<long>("relayworks.records.attention");
    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>("relayworks.worker.outbox.published");
    public static readonly Histogram<double> ConnectorDuration = Meter.CreateHistogram<double>("relayworks.connector.duration", "ms");
}
