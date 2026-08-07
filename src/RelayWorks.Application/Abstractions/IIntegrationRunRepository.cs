using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Application.IntegrationRuns;

namespace RelayWorks.Application.Abstractions;

public interface IIntegrationRunRepository
{
    Task<IntegrationRun?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken);
    Task<IntegrationRun?> FindByIdAsync(Guid runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IntegrationRun>> ListAsync(IntegrationRunQuery query,
        CancellationToken cancellationToken);
    Task AddWithOutboxMessageAsync(
        IntegrationRun run,
        string messageType,
        string messagePayload,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
