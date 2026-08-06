using RelayWorks.Domain.IntegrationRuns;

namespace RelayWorks.Domain.Tests;

public sealed class IntegrationRunTests
{
    private static readonly Guid TenantId = Guid.Parse("5d963a18-c113-4bea-b2c7-c71a121e9f4b");
    private static readonly Guid ConnectionId = Guid.Parse("857840a1-3440-431d-a696-07616926d50b");

    [Fact]
    public void Complete_marks_run_with_rejections_as_attention_required()
    {
        var run = CreateRun(10);
        run.Start();
        run.Complete(8, 2, DateTimeOffset.Parse("2026-08-06T12:01:00Z"));

        Assert.Equal(IntegrationRunStatus.CompletedWithErrors, run.Status);
        Assert.Equal(8, run.AcceptedRecords);
        Assert.Equal(2, run.RejectedRecords);
    }

    [Fact]
    public void Complete_requires_every_record_to_be_accounted_for()
    {
        var run = CreateRun(10);
        run.Start();

        Assert.Throws<ArgumentException>(() =>
            run.Complete(7, 2, DateTimeOffset.Parse("2026-08-06T12:01:00Z")));
    }

    [Fact]
    public void Tenant_and_connection_are_required()
    {
        Assert.Throws<ArgumentException>(() => IntegrationRun.Create(
            Guid.Empty,
            ConnectionId,
            IntegrationOperation.TimeEntryExport,
            "time-2026-w32",
            10,
            DateTimeOffset.Parse("2026-08-06T12:00:00Z")));
    }

    private static IntegrationRun CreateRun(int totalRecords) => IntegrationRun.Create(
        TenantId,
        ConnectionId,
        IntegrationOperation.TimeEntryExport,
        "time-2026-w32",
        totalRecords,
        DateTimeOffset.Parse("2026-08-06T12:00:00Z"));
}
