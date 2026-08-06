using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker;

public sealed class SimulatedFieldOperationsConnector : ITimeEntrySourceConnector
{
    public IEnumerable<CanonicalTimeEntryV1> Read(IntegrationRunRequestedV1 command)
    {
        for (var index = 1; index <= command.TotalRecords; index++)
        {
            yield return new CanonicalTimeEntryV1(
                command.TenantId,
                $"time-{index:000000}",
                "1",
                $"employee-{(index % 12) + 1:000}",
                index % 10 == 0 ? "" : $"project-{(index % 4) + 1:000}",
                DateOnly.FromDateTime(command.OccurredAtUtc.UtcDateTime),
                8m,
                index % 7 == 0 ? 1m : 0m,
                "REG",
                command.RunId);
        }
    }
}
