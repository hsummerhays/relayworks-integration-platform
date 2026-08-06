namespace RelayWorks.Domain.IntegrationRuns;

public sealed class IntegrationRun
{
    private IntegrationRun() => IdempotencyKey = null!;

    private IntegrationRun(
        Guid id,
        Guid tenantId,
        Guid connectionId,
        IntegrationOperation operation,
        string idempotencyKey,
        int totalRecords,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        ConnectionId = connectionId;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        TotalRecords = totalRecords;
        CreatedAtUtc = createdAtUtc;
        Status = IntegrationRunStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public IntegrationOperation Operation { get; private set; }
    public string IdempotencyKey { get; private set; }
    public int TotalRecords { get; private set; }
    public int AcceptedRecords { get; private set; }
    public int RejectedRecords { get; private set; }
    public IntegrationRunStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static IntegrationRun Create(
        Guid tenantId,
        Guid connectionId,
        IntegrationOperation operation,
        string idempotencyKey,
        int totalRecords,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (connectionId == Guid.Empty) throw new ArgumentException("Connection is required.", nameof(connectionId));
        if (totalRecords <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRecords), "A run must contain at least one record.");
        }
        if (totalRecords > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRecords), "Iteration 2 runs are limited to 10,000 records.");
        }

        return new IntegrationRun(
            Guid.NewGuid(),
            tenantId,
            connectionId,
            operation,
            idempotencyKey.Trim(),
            totalRecords,
            createdAtUtc);
    }

    public void Start()
    {
        EnsureStatus(IntegrationRunStatus.Pending);
        Status = IntegrationRunStatus.Running;
    }

    public void Complete(int acceptedRecords, int rejectedRecords, DateTimeOffset completedAtUtc)
    {
        EnsureStatus(IntegrationRunStatus.Running);
        if (acceptedRecords < 0 || rejectedRecords < 0 || acceptedRecords + rejectedRecords != TotalRecords)
        {
            throw new ArgumentException("Accepted and rejected counts must account for every submitted record.");
        }

        AcceptedRecords = acceptedRecords;
        RejectedRecords = rejectedRecords;
        CompletedAtUtc = completedAtUtc;
        Status = rejectedRecords == 0
            ? IntegrationRunStatus.Completed
            : IntegrationRunStatus.CompletedWithErrors;
    }

    public void Fail(DateTimeOffset completedAtUtc)
    {
        if (Status is not (IntegrationRunStatus.Pending or IntegrationRunStatus.Running))
        {
            throw new InvalidOperationException($"Run {Id} cannot fail from {Status}.");
        }

        Status = IntegrationRunStatus.Failed;
        CompletedAtUtc = completedAtUtc;
    }

    private void EnsureStatus(IntegrationRunStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Run {Id} must be {expected} but is {Status}.");
        }
    }
}
