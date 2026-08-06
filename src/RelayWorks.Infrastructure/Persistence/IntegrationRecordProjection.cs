namespace RelayWorks.Infrastructure.Persistence;

public sealed class IntegrationRecordProjection
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RunId { get; init; }
    public Guid TenantId { get; init; }
    public string SourceRecordId { get; init; } = string.Empty;
    public string SourceVersion { get; init; } = string.Empty;
    public string EmployeeReference { get; init; } = string.Empty;
    public string ProjectReference { get; init; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? DestinationReference { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private IntegrationRecordProjection() { }

    public static IntegrationRecordProjection Create(Guid runId, Guid tenantId, string sourceRecordId,
        string sourceVersion, string employeeReference, string projectReference, string status,
        string? errorCode, string? errorMessage, string? destinationReference, DateTimeOffset updatedAt) => new()
    {
        RunId = runId, TenantId = tenantId, SourceRecordId = sourceRecordId,
        SourceVersion = sourceVersion, EmployeeReference = employeeReference,
        ProjectReference = projectReference, Status = status, ErrorCode = errorCode,
        ErrorMessage = errorMessage, DestinationReference = destinationReference, UpdatedAtUtc = updatedAt
    };

    public void Update(string status, string? errorCode, string? errorMessage,
        string? destinationReference, DateTimeOffset updatedAt)
    {
        if (Status == "ManuallyResolved") return;
        Status = status; ErrorCode = errorCode; ErrorMessage = errorMessage;
        DestinationReference = destinationReference; UpdatedAtUtc = updatedAt;
    }

    public void Resolve(string notes, DateTimeOffset now)
    {
        if (Status is not ("UnknownOutcome" or "Rejected"))
            throw new InvalidOperationException("Only records requiring attention can be resolved.");
        if (string.IsNullOrWhiteSpace(notes)) throw new ArgumentException("Resolution notes are required.", nameof(notes));
        Status = "ManuallyResolved";
        ResolutionNotes = notes.Trim();
        UpdatedAtUtc = now;
    }
}
