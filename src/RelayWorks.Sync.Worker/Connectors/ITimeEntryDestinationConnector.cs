using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker;

public interface ITimeEntryDestinationConnector
{
    bool TryWrite(CanonicalTimeEntryV1 entry, out IReadOnlyList<string> validationErrors);
}
