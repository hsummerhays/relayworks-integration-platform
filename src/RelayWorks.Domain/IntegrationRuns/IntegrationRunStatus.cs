namespace RelayWorks.Domain.IntegrationRuns;

public enum IntegrationRunStatus
{
    Pending,
    Running,
    Completed,
    CompletedWithErrors,
    Failed
}
