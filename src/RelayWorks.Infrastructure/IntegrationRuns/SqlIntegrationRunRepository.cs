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
        IntegrationRunQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IntegrationRuns.AsNoTracking().Where(run => run.TenantId == request.TenantId);
        if (request.Status.HasValue) query = query.Where(run => run.Status == request.Status.Value);
        if (request.ConnectionId.HasValue) query = query.Where(run => run.ConnectionId == request.ConnectionId.Value);
        if (request.FromUtc.HasValue) query = query.Where(run => run.CreatedAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(run => run.CreatedAtUtc < request.ToUtc.Value);
        if (request.CursorTimestamp.HasValue && request.CursorId.HasValue)
            query = query.Where(run => run.CreatedAtUtc < request.CursorTimestamp.Value ||
                (run.CreatedAtUtc == request.CursorTimestamp.Value && run.Id.CompareTo(request.CursorId.Value) < 0));

        return await query
            .OrderByDescending(run => run.CreatedAtUtc)
            .ThenByDescending(run => run.Id)
            .Take(request.PageSize)
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
