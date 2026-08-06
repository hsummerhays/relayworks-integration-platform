using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker;

public interface ITimeEntryDestinationConnector
{
    DestinationWriteResult Write(CanonicalTimeEntryV1 entry, string idempotencyKey);
    DestinationLookupResult FindByIdempotencyKey(string idempotencyKey);
}

public enum DestinationWriteStatus { Succeeded, Rejected, ConfirmedNoCommit, UnknownOutcome }

public sealed record DestinationWriteResult(
    DestinationWriteStatus Status,
    string? DestinationReference = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record DestinationLookupResult(bool Found, string? DestinationReference = null);
