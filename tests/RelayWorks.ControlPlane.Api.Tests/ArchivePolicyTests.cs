using System.Globalization;
using RelayWorks.Domain.IntegrationRuns;
using RelayWorks.Infrastructure.Archival;
using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.ControlPlane.Api.Tests;

public sealed class ArchivePolicyTests
{
    private static readonly DateTimeOffset Cutoff = DateTimeOffset.Parse("2026-07-08T00:00:00Z", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(IntegrationRecordStatuses.UnknownOutcome)]
    [InlineData(IntegrationRecordStatuses.Rejected)]
    public void Unresolved_records_are_never_eligible(string status)
    {
        Assert.False(ArchivePolicy.IsEligible(IntegrationRunStatus.CompletedWithErrors,
            Cutoff.AddDays(-1), new[] { IntegrationRecordStatuses.Succeeded, status }, Cutoff));
    }

    [Fact]
    public void Old_terminal_run_with_only_safe_states_is_eligible()
    {
        Assert.True(ArchivePolicy.IsEligible(IntegrationRunStatus.CompletedWithErrors,
            Cutoff.AddDays(-1), new[] { IntegrationRecordStatuses.Succeeded, IntegrationRecordStatuses.ManuallyResolved }, Cutoff));
    }

    [Fact]
    public void Active_or_recent_runs_are_not_eligible()
    {
        Assert.False(ArchivePolicy.IsEligible(IntegrationRunStatus.Running,
            Cutoff.AddDays(-1), Array.Empty<string>(), Cutoff));
        Assert.False(ArchivePolicy.IsEligible(IntegrationRunStatus.Completed,
            Cutoff.AddMinutes(1), Array.Empty<string>(), Cutoff));
    }
}
