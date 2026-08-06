using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using RelayWorks.Application.Abstractions;
using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Infrastructure.Persistence;

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
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IntegrationRuns.AsNoTracking();
        if (tenantId.HasValue) query = query.Where(run => run.TenantId == tenantId.Value);

        return await query
            .OrderByDescending(run => run.CreatedAtUtc)
            .Take(200)
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
