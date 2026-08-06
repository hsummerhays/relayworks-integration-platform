namespace RelayWorks.Infrastructure.Persistence;

public sealed class OperatorAuditRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string ActorId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; init; }
}
