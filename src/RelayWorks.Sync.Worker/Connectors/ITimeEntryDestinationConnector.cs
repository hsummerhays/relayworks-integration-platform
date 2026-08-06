using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker;

public interface ITimeEntryDestinationConnector
{
    DestinationWriteResult Write(CanonicalTimeEntryV1 entry);
}

public enum DestinationWriteStatus { Succeeded, Rejected, UnknownOutcome }

public sealed record DestinationWriteResult(
    DestinationWriteStatus Status,
    string? DestinationReference = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
