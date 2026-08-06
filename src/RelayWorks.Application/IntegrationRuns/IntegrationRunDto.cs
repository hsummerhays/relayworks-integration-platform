using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Application.IntegrationRuns;

public sealed record IntegrationRunDto(
    Guid Id,
    Guid TenantId,
    Guid ConnectionId,
    IntegrationOperation Operation,
    string IdempotencyKey,
    int TotalRecords,
    int AcceptedRecords,
    int RejectedRecords,
    IntegrationRunStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    public static IntegrationRunDto FromDomain(IntegrationRun run) => new(
        run.Id,
        run.TenantId,
        run.ConnectionId,
        run.Operation,
        run.IdempotencyKey,
        run.TotalRecords,
        run.AcceptedRecords,
        run.RejectedRecords,
        run.Status,
        run.CreatedAtUtc,
        run.CompletedAtUtc);
}
