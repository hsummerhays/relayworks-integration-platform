namespace RelayWorks.Infrastructure.Archival;

public sealed class ArchiveOptions
{
    public const string SectionName = "Archive";
    public bool Enabled { get; set; }
    public bool DryRun { get; set; } = true;
    public string BlobServiceUri { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "integration-history";
    public int SuccessfulRunRetentionDays { get; set; } = 30;
    public int DispatchedOutboxRetentionDays { get; set; } = 14;
    public int BatchSize { get; set; } = 25;
    public int IntervalMinutes { get; set; } = 60;
}
