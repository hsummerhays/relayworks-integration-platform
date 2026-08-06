namespace RelayWorks.Contracts.TimeEntries;

public sealed record CanonicalTimeEntryV1(
    Guid TenantId,
    string SourceRecordId,
    string SourceVersion,
    string EmployeeReference,
    string ProjectReference,
    DateOnly WorkDate,
    decimal RegularHours,
    decimal OvertimeHours,
    string LaborCode,
    Guid CorrelationId);
