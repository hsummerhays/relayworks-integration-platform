namespace RelayWorks.Contracts.IntegrationRuns;

public sealed record IntegrationRunRequestedV1(
    Guid MessageId,
    Guid RunId,
    Guid TenantId,
    Guid ConnectionId,
    string Operation,
    int TotalRecords,
    DateTimeOffset OccurredAtUtc,
    ConnectorExecutionProfileV1? ConnectorProfile = null);

public sealed record ConnectorExecutionProfileV1(
    string Provider,
    bool SupportsIdempotencyKey,
    bool SupportsReadAfterWrite,
    int MaxConfirmedNoCommitRetries,
    string ConfigurationVersion,
    SecretLocatorV1 Secret);

public sealed record SecretLocatorV1(
    Uri VaultUri,
    string SecretName,
    string? SecretVersion = null,
    string? RoutingKey = null);

public sealed record IntegrationRunCompletedV1(
    Guid MessageId,
    Guid RunId,
    Guid TenantId,
    int AcceptedRecords,
    int RejectedRecords,
    DateTimeOffset OccurredAtUtc);

public sealed record IntegrationRunFailedV1(
    Guid MessageId,
    Guid RunId,
    Guid TenantId,
    string ErrorCode,
    string ErrorMessage,
    DateTimeOffset OccurredAtUtc);

public sealed record IntegrationRecordResultV1(
    string SourceRecordId,
    string SourceVersion,
    string EmployeeReference,
    string ProjectReference,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string? DestinationReference,
    DateTimeOffset OccurredAtUtc);

public sealed record IntegrationRecordResultsReportedV1(
    Guid MessageId,
    Guid RunId,
    Guid TenantId,
    IReadOnlyList<IntegrationRecordResultV1> Records,
    DateTimeOffset OccurredAtUtc);
