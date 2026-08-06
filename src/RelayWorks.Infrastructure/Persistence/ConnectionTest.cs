namespace RelayWorks.Infrastructure.Persistence;

public sealed class ConnectionTest
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ConnectionId { get; init; }
    public string ConfigurationVersion { get; init; } = string.Empty;
    public string Status { get; private set; } = "Pending";
    public string? FailureCategory { get; private set; }
    public string? SafeMessage { get; private set; }
    public string RequestedBy { get; init; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public long? DurationMilliseconds { get; private set; }

    public void Complete(string status, string? category, string? message, TimeSpan duration, DateTimeOffset now)
    {
        if (Status is "Succeeded" or "Failed" or "TimedOut" or "Canceled") return;
        Status = status; FailureCategory = category; SafeMessage = message;
        DurationMilliseconds = (long)duration.TotalMilliseconds; CompletedAtUtc = now;
    }
}
