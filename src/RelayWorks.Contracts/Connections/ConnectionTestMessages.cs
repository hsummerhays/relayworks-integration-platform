using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Contracts.Connections;

public sealed record ConnectionTestRequestedV1(Guid MessageId, Guid TestId, Guid TenantId,
    Guid ConnectionId, ConnectorExecutionProfileV1 ConnectorProfile, DateTimeOffset OccurredAtUtc);

public sealed record ConnectionTestCompletedV1(Guid MessageId, Guid TestId, Guid TenantId,
    Guid ConnectionId, string Status, string? FailureCategory, string? SafeMessage,
    TimeSpan Duration, DateTimeOffset OccurredAtUtc);
