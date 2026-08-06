using System.Collections.Concurrent;
using RelayWorks.Application.Abstractions;
using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Infrastructure.IntegrationRuns;

public sealed class InMemoryIntegrationRunRepository : IIntegrationRunRepository
{
    private readonly ConcurrentDictionary<string, IntegrationRun> _runs =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IntegrationRun?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _runs.TryGetValue(idempotencyKey, out var run);
        return Task.FromResult(run);
    }

    public Task<IReadOnlyList<IntegrationRun>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IntegrationRun> runs = _runs.Values
            .OrderByDescending(run => run.CreatedAtUtc)
            .ToList();
        return Task.FromResult(runs);
    }

    public Task AddAsync(IntegrationRun run, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_runs.TryAdd(run.IdempotencyKey, run))
        {
            throw new InvalidOperationException($"A run with idempotency key '{run.IdempotencyKey}' already exists.");
        }

        return Task.CompletedTask;
    }
}
