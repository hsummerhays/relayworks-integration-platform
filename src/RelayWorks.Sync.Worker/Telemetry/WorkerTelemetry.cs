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
    public static readonly Counter<long> ConnectorRetries = Meter.CreateCounter<long>("relayworks.connector.retries");
    public static readonly Counter<long> ConnectorThrottled = Meter.CreateCounter<long>("relayworks.connector.throttled");
    public static readonly Counter<long> CircuitOpened = Meter.CreateCounter<long>("relayworks.connector.circuit.opened");
    public static readonly Histogram<double> ThrottleWait = Meter.CreateHistogram<double>("relayworks.connector.throttle.wait", "ms");
    public static readonly Counter<long> RetentionRowsDeleted = Meter.CreateCounter<long>("relayworks.retention.rows.deleted");
    public static readonly Counter<long> RetentionFailures = Meter.CreateCounter<long>("relayworks.retention.failures");
}
