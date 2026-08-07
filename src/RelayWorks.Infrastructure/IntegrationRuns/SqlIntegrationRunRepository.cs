using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using RelayWorks.Application.Abstractions;
using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Infrastructure.Persistence;
using RelayWorks.Application.IntegrationRuns;

namespace RelayWorks.Infrastructure.IntegrationRuns;

public sealed class SqlIntegrationRunRepository(
    RelayWorksDbContext dbContext,
    TimeProvider timeProvider) : IIntegrationRunRepository
{
    public Task<IntegrationRun?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.IntegrationRuns.FirstOrDefaultAsync(
            run => run.TenantId == tenantId && run.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<IntegrationRun?> FindByIdAsync(Guid runId, CancellationToken cancellationToken) =>
        dbContext.IntegrationRuns.FirstOrDefaultAsync(run => run.Id == runId, cancellationToken);

    public async Task<IReadOnlyList<IntegrationRun>> ListAsync(
        IntegrationRunQuery query,
        CancellationToken cancellationToken)
    {
        var runsQuery = dbContext.IntegrationRuns.AsNoTracking().Where(run => run.TenantId == query.TenantId);
        if (query.Status.HasValue) runsQuery = runsQuery.Where(run => run.Status == query.Status.Value);
        if (query.ConnectionId.HasValue) runsQuery = runsQuery.Where(run => run.ConnectionId == query.ConnectionId.Value);
        if (query.FromUtc.HasValue) runsQuery = runsQuery.Where(run => run.CreatedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) runsQuery = runsQuery.Where(run => run.CreatedAtUtc < query.ToUtc.Value);
        if (query.CursorTimestamp.HasValue && query.CursorId.HasValue)
            runsQuery = runsQuery.Where(run => run.CreatedAtUtc < query.CursorTimestamp.Value ||
                (run.CreatedAtUtc == query.CursorTimestamp.Value && run.Id.CompareTo(query.CursorId.Value) < 0));

        return await runsQuery
            .OrderByDescending(run => run.CreatedAtUtc)
            .ThenByDescending(run => run.Id)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddWithOutboxMessageAsync(
        IntegrationRun run,
        string messageType,
        string messagePayload,
        CancellationToken cancellationToken)
    {
        dbContext.IntegrationRuns.Add(run);
        dbContext.OutboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(),
            messageType,
            messagePayload,
            timeProvider.GetUtcNow()));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            dbContext.ChangeTracker.Clear();
            throw new DuplicateIntegrationRunException(exception);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
