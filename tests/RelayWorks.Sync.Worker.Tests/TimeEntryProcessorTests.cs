using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker;
using RelayWorks.Sync.Worker.Persistence;
using RelayWorks.Sync.Worker.Resilience;
using Microsoft.Extensions.Options;

namespace RelayWorks.Sync.Worker.Tests;

public sealed class TimeEntryProcessorTests
{
    [Fact]
    public async Task Read_after_write_recovers_an_ambiguous_committed_record()
    {
        var options = new DbContextOptionsBuilder<WorkerLedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new WorkerLedgerDbContext(options);
        var processor = new TimeEntryProcessor(new SimulatedFieldOperationsConnector(),
            new FixedConnectorFactory(new SimulatedAccountingConnector()), db, Resilience());
        var now = DateTimeOffset.Parse("2026-08-06T12:00:00Z", CultureInfo.InvariantCulture);
        var command = new IntegrationRunRequestedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "TimeEntryExport", 17, now,
            new ConnectorExecutionProfileV1("SimulatedAccounting", true, true, 2, "test-v1",
                new SecretLocatorV1(new Uri("https://test.vault.azure.net"), "accounting")));

        var result = await processor.ProcessAsync(command, now.AddMinutes(1), TestContext.Current.CancellationToken);
        var recovered = await db.RecordDeliveries.SingleAsync(x => x.SourceRecordId == "time-000017", TestContext.Current.CancellationToken);

        Assert.Equal(15, result.AcceptedRecords);
        Assert.Equal(RecordDeliveryState.Succeeded, recovered.State);
        Assert.NotNull(recovered.DestinationReference);
    }

    [Fact]
    public async Task Redelivered_command_does_not_write_destination_twice()
    {
        var options = new DbContextOptionsBuilder<WorkerLedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new WorkerLedgerDbContext(options);
        var destination = new CountingDestination();
        var processor = new TimeEntryProcessor(new SimulatedFieldOperationsConnector(),
            new FixedConnectorFactory(destination), db, Resilience());
        var command = new IntegrationRunRequestedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "TimeEntryExport", 3, DateTimeOffset.Parse("2026-08-06T12:00:00Z", CultureInfo.InvariantCulture),
            new ConnectorExecutionProfileV1("Test", true, false, 0, "test-v1",
                new SecretLocatorV1(new Uri("https://test.vault.azure.net"), "accounting")));

        var first = await processor.ProcessAsync(command, command.OccurredAtUtc.AddMinutes(1), TestContext.Current.CancellationToken);
        var second = await processor.ProcessAsync(command, command.OccurredAtUtc.AddMinutes(2), TestContext.Current.CancellationToken);

        Assert.Equal(3, first.AcceptedRecords);
        Assert.Equal(3, second.AcceptedRecords);
        Assert.Equal(3, destination.Writes);
    }

    private sealed class CountingDestination : ITimeEntryDestinationConnector
    {
        public int Writes { get; private set; }
        public Task<DestinationWriteResult> WriteAsync(
            RelayWorks.Contracts.TimeEntries.CanonicalTimeEntryV1 entry, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            Writes++;
            return Task.FromResult(new DestinationWriteResult(DestinationWriteStatus.Succeeded,
                $"destination:{entry.SourceRecordId}"));
        }
        public Task<DestinationLookupResult> FindByIdempotencyKeyAsync(string idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult(new DestinationLookupResult(false));
        public Task<ConnectorHealthResult> TestConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectorHealthResult(true));
    }

    private sealed class FixedConnectorFactory(ITimeEntryDestinationConnector connector)
        : ITimeEntryDestinationConnectorFactory
    {
        public Task<ITimeEntryDestinationConnector> CreateAsync(ConnectorExecutionProfileV1 profile,
            CancellationToken cancellationToken) => Task.FromResult(connector);
    }

    private static DestinationResilienceExecutor Resilience()
    {
        var options = Options.Create(new ConnectorResilienceOptions
        {
            MaxConcurrentRequestsPerConnection = 10,
            RequestsPerSecondPerConnection = 1000,
            BurstCapacityPerConnection = 1000,
            BaseRetryDelayMilliseconds = 1,
            CircuitFailureThreshold = 100
        });
        var delay = new NoDelay();
        return new DestinationResilienceExecutor(
            new ConnectionExecutionGate(options, TimeProvider.System, delay),
            options, TimeProvider.System, delay);
    }

    private sealed class NoDelay : IResilienceDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
