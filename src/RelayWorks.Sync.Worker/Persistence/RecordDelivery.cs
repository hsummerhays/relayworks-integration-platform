namespace RelayWorks.Sync.Worker.Persistence;

public enum RecordDeliveryState
{
    Processing,
    Succeeded,
    Rejected,
    RetryableFailure,
    UnknownOutcome
}

public sealed class RecordDelivery
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public Guid RunId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string SourceRecordId { get; private set; } = string.Empty;
    public string SourceVersion { get; private set; } = string.Empty;
    public string CanonicalFingerprint { get; private set; } = string.Empty;
    public string EmployeeReference { get; private set; } = string.Empty;
    public string ProjectReference { get; private set; } = string.Empty;
    public RecordDeliveryState State { get; private set; }
    public int AttemptCount { get; private set; }
    public string? DestinationReference { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private RecordDelivery() { }

    public static RecordDelivery Start(Guid tenantId, Guid connectionId, Guid runId, string operation,
        string sourceRecordId, string sourceVersion, string fingerprint, string employeeReference,
        string projectReference, DateTimeOffset now) => new()
    {
        TenantId = tenantId, ConnectionId = connectionId, RunId = runId, Operation = operation,
        SourceRecordId = sourceRecordId, SourceVersion = sourceVersion,
        CanonicalFingerprint = fingerprint, EmployeeReference = employeeReference,
        ProjectReference = projectReference, State = RecordDeliveryState.Processing,
        AttemptCount = 1, UpdatedAtUtc = now
    };

    public void Finish(RecordDeliveryState state, string? destinationReference, string? errorCode,
        string? errorMessage, DateTimeOffset now)
    {
        State = state;
        DestinationReference = destinationReference;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        UpdatedAtUtc = now;
    }

    public void RecordAttempt(DateTimeOffset now)
    {
        AttemptCount++;
        UpdatedAtUtc = now;
    }
}
