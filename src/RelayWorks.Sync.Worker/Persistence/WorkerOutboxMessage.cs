namespace RelayWorks.Sync.Worker.Persistence;

public sealed class WorkerOutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; init; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }
}
