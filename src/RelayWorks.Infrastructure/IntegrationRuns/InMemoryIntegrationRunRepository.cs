using System.Collections.Concurrent;
using RelayWorks.Application.Abstractions;
using RelayWorks.Application.IntegrationRuns;
using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Infrastructure.IntegrationRuns;

public sealed class InMemoryIntegrationRunRepository : IIntegrationRunRepository
{
    private readonly ConcurrentDictionary<Guid, IntegrationRun> _runs = new();

    public Task<IntegrationRun?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var run = _runs.Values.FirstOrDefault(candidate =>
            candidate.TenantId == tenantId && candidate.IdempotencyKey == idempotencyKey);
        return Task.FromResult(run);
    }

    public Task<IntegrationRun?> FindByIdAsync(Guid runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public Task<IReadOnlyList<IntegrationRun>> ListAsync(
        IntegrationRunQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runs = _runs.Values.Where(run => run.TenantId == query.TenantId);
        if (query.Status.HasValue) runs = runs.Where(run => run.Status == query.Status.Value);
        if (query.ConnectionId.HasValue) runs = runs.Where(run => run.ConnectionId == query.ConnectionId.Value);
        if (query.FromUtc.HasValue) runs = runs.Where(run => run.CreatedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) runs = runs.Where(run => run.CreatedAtUtc < query.ToUtc.Value);
        if (query.CursorTimestamp.HasValue && query.CursorId.HasValue)
            runs = runs.Where(run => run.CreatedAtUtc < query.CursorTimestamp.Value ||
                (run.CreatedAtUtc == query.CursorTimestamp.Value && run.Id.CompareTo(query.CursorId.Value) < 0));

        IReadOnlyList<IntegrationRun> page = runs
            .OrderByDescending(run => run.CreatedAtUtc)
            .ThenByDescending(run => run.Id)
            .Take(query.PageSize)
            .ToList();
        return Task.FromResult(page);
    }

    public Task AddWithOutboxMessageAsync(
        IntegrationRun run,
        string messageType,
        string messagePayload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_runs.TryAdd(run.Id, run))
        {
            throw new InvalidOperationException($"A run with id '{run.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
