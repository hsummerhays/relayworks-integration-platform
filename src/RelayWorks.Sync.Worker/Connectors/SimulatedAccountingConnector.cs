using RelayWorks.Contracts.TimeEntries;

namespace RelayWorks.Sync.Worker;

public sealed class SimulatedAccountingConnector : ITimeEntryDestinationConnector
{
    public bool TryWrite(CanonicalTimeEntryV1 entry, out IReadOnlyList<string> validationErrors)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(entry.EmployeeReference)) errors.Add("EMPLOYEE_REQUIRED");
        if (string.IsNullOrWhiteSpace(entry.ProjectReference)) errors.Add("PROJECT_REQUIRED");
        if (entry.RegularHours < 0 || entry.OvertimeHours < 0) errors.Add("HOURS_NEGATIVE");
        if (entry.RegularHours + entry.OvertimeHours > 24) errors.Add("HOURS_EXCEED_DAY");
        if (string.IsNullOrWhiteSpace(entry.LaborCode)) errors.Add("LABOR_CODE_REQUIRED");
        validationErrors = errors;
        return errors.Count == 0;
    }
}
