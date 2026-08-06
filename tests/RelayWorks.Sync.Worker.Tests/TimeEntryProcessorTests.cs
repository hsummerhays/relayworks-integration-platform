using Microsoft.EntityFrameworkCore;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker;
using RelayWorks.Sync.Worker.Persistence;

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
            new FixedConnectorFactory(new SimulatedAccountingConnector()), db);
        var now = DateTimeOffset.Parse("2026-08-06T12:00:00Z");
        var command = new IntegrationRunRequestedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "TimeEntryExport", 17, now,
            new ConnectorExecutionProfileV1("SimulatedAccounting", true, true, 2, "test-v1",
                new SecretLocatorV1(new Uri("https://test.vault.azure.net"), "accounting")));

        var result = await processor.ProcessAsync(command, now.AddMinutes(1), default);
        var recovered = await db.RecordDeliveries.SingleAsync(x => x.SourceRecordId == "time-000017");

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
            new FixedConnectorFactory(destination), db);
        var command = new IntegrationRunRequestedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "TimeEntryExport", 3, DateTimeOffset.Parse("2026-08-06T12:00:00Z"),
            new ConnectorExecutionProfileV1("Test", true, false, 0, "test-v1",
                new SecretLocatorV1(new Uri("https://test.vault.azure.net"), "accounting")));

        var first = await processor.ProcessAsync(command, command.OccurredAtUtc.AddMinutes(1), default);
        var second = await processor.ProcessAsync(command, command.OccurredAtUtc.AddMinutes(2), default);

        Assert.Equal(3, first.AcceptedRecords);
        Assert.Equal(3, second.AcceptedRecords);
        Assert.Equal(3, destination.Writes);
    }

    private sealed class CountingDestination : ITimeEntryDestinationConnector
    {
        public int Writes { get; private set; }
        public DestinationWriteResult Write(RelayWorks.Contracts.TimeEntries.CanonicalTimeEntryV1 entry, string idempotencyKey)
        {
            Writes++;
            return new(DestinationWriteStatus.Succeeded, $"destination:{entry.SourceRecordId}");
        }
        public DestinationLookupResult FindByIdempotencyKey(string idempotencyKey) => new(false);
    }

    private sealed class FixedConnectorFactory(ITimeEntryDestinationConnector connector)
        : ITimeEntryDestinationConnectorFactory
    {
        public Task<ITimeEntryDestinationConnector> CreateAsync(ConnectorExecutionProfileV1 profile,
            CancellationToken cancellationToken) => Task.FromResult(connector);
    }
}
