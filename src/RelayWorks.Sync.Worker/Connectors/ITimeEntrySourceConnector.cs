using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker;

public interface ITimeEntrySourceConnector
{
    IEnumerable<CanonicalTimeEntryV1> Read(IntegrationRunRequestedV1 command);
}
