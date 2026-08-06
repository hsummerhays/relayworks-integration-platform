using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker;

public sealed class TimeEntryProcessor(
    ITimeEntrySourceConnector sourceConnector,
    ITimeEntryDestinationConnector destinationConnector)
{
    public IntegrationRunCompletedV1 Process(IntegrationRunRequestedV1 command, DateTimeOffset completedAtUtc)
    {
        var accepted = 0;
        var rejected = 0;

        foreach (var entry in sourceConnector.Read(command))
        {
            if (destinationConnector.TryWrite(entry, out _)) accepted++;
            else rejected++;
        }

        return new IntegrationRunCompletedV1(
            Guid.NewGuid(),
            command.RunId,
            command.TenantId,
            accepted,
            rejected,
            completedAtUtc);
    }
}
