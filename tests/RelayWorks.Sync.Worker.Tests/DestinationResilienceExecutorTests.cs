using Microsoft.Extensions.Options;
using RelayWorks.Contracts.TimeEntries;
using RelayWorks.Sync.Worker;
using RelayWorks.Sync.Worker.Resilience;

namespace RelayWorks.Sync.Worker.Tests;

public sealed class DestinationResilienceExecutorTests
{
    [Fact]
    public async Task Retries_only_confirmed_no_commit_and_honors_retry_after()
    {
        var delay = new RecordingDelay();
        var executor = CreateExecutor(delay);
        var connector = new SequenceConnector(
            new(DestinationWriteStatus.ConfirmedNoCommit, ErrorCode: "HTTP_429",
                RetryAfter: TimeSpan.FromSeconds(7)),
            new(DestinationWriteStatus.Succeeded, "destination:1"));
        var attempts = 0;

        var result = await executor.WriteAsync(Guid.NewGuid(), "Test", 2, connector, Entry(), "key",
            () => attempts++, TestContext.Current.CancellationToken);

        Assert.Equal(DestinationWriteStatus.Succeeded, result.Status);
        Assert.Equal(2, connector.Writes);
        Assert.Equal(1, attempts);
        Assert.Single(delay.Delays);
        Assert.True(delay.Delays[0] >= TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task Never_retries_an_unknown_outcome()
    {
        var delay = new RecordingDelay();
        var executor = CreateExecutor(delay);
        var connector = new SequenceConnector(new DestinationWriteResult(DestinationWriteStatus.UnknownOutcome,
            ErrorCode: "TIMEOUT_AFTER_SEND"));

        var result = await executor.WriteAsync(Guid.NewGuid(), "Test", 5, connector, Entry(), "key",
            () => throw new InvalidOperationException("No retry expected."), TestContext.Current.CancellationToken);

        Assert.Equal(DestinationWriteStatus.UnknownOutcome, result.Status);
        Assert.Equal(1, connector.Writes);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task Open_circuit_stops_calls_after_confirmed_failure_threshold()
    {
        var delay = new RecordingDelay();
        var options = Options.Create(new ConnectorResilienceOptions
        {
            MaxConcurrentRequestsPerConnection = 1, RequestsPerSecondPerConnection = 1000,
            BurstCapacityPerConnection = 1000, BaseRetryDelayMilliseconds = 1,
            CircuitFailureThreshold = 1, CircuitBreakSeconds = 30
        });
        var executor = new DestinationResilienceExecutor(
            new ConnectionExecutionGate(options, TimeProvider.System, delay), options, TimeProvider.System, delay);
        var connector = new SequenceConnector(new DestinationWriteResult(DestinationWriteStatus.ConfirmedNoCommit));

        var result = await executor.WriteAsync(Guid.NewGuid(), "Test", 5, connector, Entry(), "key", () => { }, TestContext.Current.CancellationToken);

        Assert.Equal("CIRCUIT_OPEN", result.ErrorCode);
        Assert.Equal(1, connector.Writes);
    }

    private static DestinationResilienceExecutor CreateExecutor(IResilienceDelay delay)
    {
        var options = Options.Create(new ConnectorResilienceOptions
        {
            MaxConcurrentRequestsPerConnection = 2,
            RequestsPerSecondPerConnection = 1000,
            BurstCapacityPerConnection = 1000,
            BaseRetryDelayMilliseconds = 1,
            CircuitFailureThreshold = 100
        });
        return new(new ConnectionExecutionGate(options, TimeProvider.System, delay),
            options, TimeProvider.System, delay);
    }

    private static CanonicalTimeEntryV1 Entry() => new(Guid.NewGuid(), "record-1", "1",
        "employee-1", "project-1", new DateOnly(2026, 8, 7), 8, 0, "REG", Guid.NewGuid());

    private sealed class RecordingDelay : IResilienceDelay
    {
        public List<TimeSpan> Delays { get; } = [];
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceConnector(params DestinationWriteResult[] results)
        : ITimeEntryDestinationConnector
    {
        private readonly Queue<DestinationWriteResult> _results = new(results);
        public int Writes { get; private set; }
        public Task<DestinationWriteResult> WriteAsync(CanonicalTimeEntryV1 entry, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            Writes++;
            return Task.FromResult(_results.Dequeue());
        }
        public Task<DestinationLookupResult> FindByIdempotencyKeyAsync(string idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult(new DestinationLookupResult(false));
        public Task<ConnectorHealthResult> TestConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectorHealthResult(true));
    }
}
