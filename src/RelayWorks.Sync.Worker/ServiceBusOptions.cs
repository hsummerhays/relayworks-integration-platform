namespace RelayWorks.Sync.Worker;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";
    public string? FullyQualifiedNamespace { get; init; }
    public string? ConnectionString { get; init; }
    public string CommandsQueue { get; init; } = "integration-commands";
    public string EventsTopic { get; init; } = "integration-events";
}
