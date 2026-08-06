using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Infrastructure.Persistence;

public sealed class ConnectionProfile
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public bool SupportsIdempotencyKey { get; private set; }
    public bool SupportsReadAfterWrite { get; private set; }
    public int MaxConfirmedNoCommitRetries { get; private set; }
    public string SecretReference { get; private set; } = string.Empty;
    public string ConfigurationVersion { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private ConnectionProfile() { }

    public static ConnectionProfile Create(Guid id, Guid tenantId, string name, string provider,
        bool supportsIdempotencyKey, bool supportsReadAfterWrite, int maxRetries,
        string secretReference, DateTimeOffset now)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Connection and tenant IDs are required.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Name and provider are required.");
        if (maxRetries is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(maxRetries));
        if (string.IsNullOrWhiteSpace(secretReference)) throw new ArgumentException("A Key Vault secret reference is required.");
        return new ConnectionProfile
        {
            Id = id, TenantId = tenantId, Name = name.Trim(), Provider = provider.Trim(),
            SupportsIdempotencyKey = supportsIdempotencyKey, SupportsReadAfterWrite = supportsReadAfterWrite,
            MaxConfirmedNoCommitRetries = maxRetries, SecretReference = secretReference.Trim(),
            ConfigurationVersion = Guid.NewGuid().ToString("N"), IsActive = true, UpdatedAtUtc = now
        };
    }

    public ConnectorExecutionProfileV1 Snapshot() => new(Provider, SupportsIdempotencyKey,
        SupportsReadAfterWrite, MaxConfirmedNoCommitRetries, ConfigurationVersion, SecretReference);
}
