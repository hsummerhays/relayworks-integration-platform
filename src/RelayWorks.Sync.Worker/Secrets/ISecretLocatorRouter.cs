using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker.Secrets;

public interface ISecretLocatorRouter
{
    SecretLocatorV1 Route(SecretLocatorV1 locator);
}

public sealed class ConfiguredSecretLocatorRouter(IConfiguration configuration) : ISecretLocatorRouter
{
    public SecretLocatorV1 Route(SecretLocatorV1 locator)
    {
        if (string.IsNullOrWhiteSpace(locator.RoutingKey)) return locator;
        var overrideUri = configuration[$"SecretVaultRouting:{locator.RoutingKey}"];
        return Uri.TryCreate(overrideUri, UriKind.Absolute, out var vaultUri)
            ? locator with { VaultUri = vaultUri }
            : locator;
    }
}
