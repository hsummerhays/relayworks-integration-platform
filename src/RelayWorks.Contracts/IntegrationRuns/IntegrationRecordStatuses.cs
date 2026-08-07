namespace RelayWorks.Contracts.IntegrationRuns;

public static class IntegrationRecordStatuses
{
    public const string Processing = nameof(Processing);
    public const string Succeeded = nameof(Succeeded);
    public const string Rejected = nameof(Rejected);
    public const string RetryableFailure = nameof(RetryableFailure);
    public const string UnknownOutcome = nameof(UnknownOutcome);
    public const string ManuallyResolved = nameof(ManuallyResolved);
}
