namespace RelayWorks.Sync.Worker.Persistence;

public sealed class WorkerInboxMessage
{
    public Guid MessageId { get; init; }
    public DateTimeOffset ProcessedAtUtc { get; init; }
}
