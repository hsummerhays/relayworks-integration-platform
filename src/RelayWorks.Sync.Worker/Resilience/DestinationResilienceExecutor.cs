using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RelayWorks.Contracts.TimeEntries;
using RelayWorks.Sync.Worker.Telemetry;

namespace RelayWorks.Sync.Worker.Resilience;

public sealed class DestinationResilienceExecutor(ConnectionExecutionGate gate,
    IOptions<ConnectorResilienceOptions> options, TimeProvider timeProvider,
    IResilienceDelay delay)
{
    private readonly ConnectorResilienceOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, CircuitState> _circuits = new();

    public Task<DestinationLookupResult> LookupAsync(Guid connectionId, string provider,
        ITimeEntryDestinationConnector connector, string idempotencyKey,
        CancellationToken cancellationToken) => gate.ExecuteAsync(connectionId, provider,
            token => connector.FindByIdempotencyKeyAsync(idempotencyKey, token), cancellationToken);

    public async Task<DestinationWriteResult> WriteAsync(Guid connectionId, string provider,
        int maxSafeRetries, ITimeEntryDestinationConnector connector, CanonicalTimeEntryV1 entry,
        string idempotencyKey, Action recordAttempt, CancellationToken cancellationToken)
    {
        var circuit = _circuits.GetOrAdd(connectionId, _ => new CircuitState());
        for (var attempt = 0; ; attempt++)
        {
            if (IsOpen(circuit))
                return new(DestinationWriteStatus.ConfirmedNoCommit, ErrorCode: "CIRCUIT_OPEN",
                    ErrorMessage: "Destination calls are paused after repeated confirmed failures.");

            var result = await gate.ExecuteAsync(connectionId, provider,
                token => connector.WriteAsync(entry, idempotencyKey, token), cancellationToken);
            if (result.Status != DestinationWriteStatus.ConfirmedNoCommit)
            {
                ResetCircuit(circuit);
                return result;
            }

            RecordConfirmedFailure(circuit, provider);
            if (attempt >= maxSafeRetries) return result;

            recordAttempt();
            WorkerTelemetry.ConnectorRetries.Add(1, new("provider", provider),
                new("reason", result.RetryAfter.HasValue ? "rate-limited" : "transient"));
            var backoff = TimeSpan.FromMilliseconds(Math.Min(
                _options.MaxRetryDelaySeconds * 1000d,
                _options.BaseRetryDelayMilliseconds * Math.Pow(2, attempt)));
            var jittered = TimeSpan.FromMilliseconds(backoff.TotalMilliseconds *
                (0.8 + Random.Shared.NextDouble() * 0.4));
            var retryAfter = result.RetryAfter is { } requested && requested > jittered ? requested : jittered;
            await delay.WaitAsync(retryAfter, cancellationToken);
        }
    }

    private bool IsOpen(CircuitState state)
    {
        lock (state.Sync)
        {
            if (state.OpenUntilUtc is not { } until) return false;
            if (until > timeProvider.GetUtcNow()) return true;
            state.OpenUntilUtc = null;
            state.ConsecutiveFailures = 0;
            return false;
        }
    }

    private void RecordConfirmedFailure(CircuitState state, string provider)
    {
        lock (state.Sync)
        {
            state.ConsecutiveFailures++;
            if (state.ConsecutiveFailures < Math.Max(1, _options.CircuitFailureThreshold)) return;
            state.OpenUntilUtc = timeProvider.GetUtcNow().AddSeconds(_options.CircuitBreakSeconds);
            WorkerTelemetry.CircuitOpened.Add(1, new KeyValuePair<string, object?>("provider", provider));
        }
    }

    private static void ResetCircuit(CircuitState state)
    {
        lock (state.Sync) { state.ConsecutiveFailures = 0; state.OpenUntilUtc = null; }
    }

    private sealed class CircuitState
    {
        public object Sync { get; } = new();
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? OpenUntilUtc { get; set; }
    }
}
