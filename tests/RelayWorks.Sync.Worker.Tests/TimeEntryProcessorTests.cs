using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker;

namespace RelayWorks.Sync.Worker.Tests;

public sealed class TimeEntryProcessorTests
{
    [Fact]
    public void Process_accounts_for_valid_and_rejected_time_entries()
    {
        var command = new IntegrationRunRequestedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "TimeEntryExport",
            20,
            DateTimeOffset.Parse("2026-08-06T12:00:00Z"));

        var result = new TimeEntryProcessor(
            new SimulatedFieldOperationsConnector(),
            new SimulatedAccountingConnector()).Process(
            command,
            DateTimeOffset.Parse("2026-08-06T12:01:00Z"));

        Assert.Equal(18, result.AcceptedRecords);
        Assert.Equal(2, result.RejectedRecords);
    }
}
