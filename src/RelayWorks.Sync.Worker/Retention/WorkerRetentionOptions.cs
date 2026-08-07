namespace RelayWorks.Sync.Worker.Retention;

public sealed class WorkerRetentionOptions
{
    public const string SectionName = "Retention";
    public bool Enabled { get; set; }
    public bool DryRun { get; set; } = true;
    public int DispatchedOutboxDays { get; set; } = 14;
    public int CompletedInboxDays { get; set; } = 90;
    public int IntervalMinutes { get; set; } = 60;
}
