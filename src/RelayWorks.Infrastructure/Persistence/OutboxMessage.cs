namespace RelayWorks.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAtUtc)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? DispatchedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }

    public void MarkDispatched(DateTimeOffset dispatchedAtUtc) => DispatchedAtUtc = dispatchedAtUtc;
    public void RecordAttempt() => AttemptCount++;
}
