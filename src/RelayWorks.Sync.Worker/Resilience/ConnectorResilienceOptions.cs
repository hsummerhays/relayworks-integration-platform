namespace RelayWorks.Sync.Worker.Resilience;

public sealed class ConnectorResilienceOptions
{
    public const string SectionName = "ConnectorResilience";
    public int MaxConcurrentRequestsPerConnection { get; set; } = 2;
    public int RequestsPerSecondPerConnection { get; set; } = 5;
    public int BurstCapacityPerConnection { get; set; } = 5;
    public int BaseRetryDelayMilliseconds { get; set; } = 500;
    public int MaxRetryDelaySeconds { get; set; } = 30;
    public int CircuitFailureThreshold { get; set; } = 5;
    public int CircuitBreakSeconds { get; set; } = 30;
}
