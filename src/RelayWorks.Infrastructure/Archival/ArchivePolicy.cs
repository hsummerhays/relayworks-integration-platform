using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Infrastructure.Archival;

public static class ArchivePolicy
{
    public static bool IsRunEligible(IntegrationRunStatus status, DateTimeOffset? completedAtUtc,
        DateTimeOffset cutoff) =>
        completedAtUtc.HasValue && completedAtUtc < cutoff &&
        status is IntegrationRunStatus.Completed or IntegrationRunStatus.CompletedWithErrors or IntegrationRunStatus.Failed;

    public static bool AreRecordsEligible(IEnumerable<string> recordStatuses) =>
        !recordStatuses.Any(value => value is IntegrationRecordStatuses.Rejected or IntegrationRecordStatuses.UnknownOutcome);

    public static bool IsEligible(IntegrationRunStatus status, DateTimeOffset? completedAtUtc,
        IEnumerable<string> recordStatuses, DateTimeOffset cutoff) =>
        IsRunEligible(status, completedAtUtc, cutoff) && AreRecordsEligible(recordStatuses);
}
