using Microsoft.EntityFrameworkCore;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker;
using RelayWorks.Sync.Worker.Persistence;

namespace RelayWorks.Sync.Worker.Tests;

public sealed class TimeEntryProcessorTests
{
    [Fact]
    public async Task Redelivered_command_does_not_write_destination_twice()
    {
        var options = new DbContextOptionsBuilder<WorkerLedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new WorkerLedgerDbContext(options);
        var destination = new CountingDestination();
        var processor = new TimeEntryProcessor(new SimulatedFieldOperationsConnector(), destination, db);
        var command = new IntegrationRunRequestedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "TimeEntryExport", 3, DateTimeOffset.Parse("2026-08-06T12:00:00Z"));

        var first = await processor.ProcessAsync(command, command.OccurredAtUtc.AddMinutes(1), default);
        var second = await processor.ProcessAsync(command, command.OccurredAtUtc.AddMinutes(2), default);

        Assert.Equal(3, first.AcceptedRecords);
        Assert.Equal(3, second.AcceptedRecords);
        Assert.Equal(3, destination.Writes);
    }

    private sealed class CountingDestination : ITimeEntryDestinationConnector
    {
        public int Writes { get; private set; }
        public DestinationWriteResult Write(RelayWorks.Contracts.TimeEntries.CanonicalTimeEntryV1 entry)
        {
            Writes++;
            return new(DestinationWriteStatus.Succeeded, $"destination:{entry.SourceRecordId}");
        }
    }
}
