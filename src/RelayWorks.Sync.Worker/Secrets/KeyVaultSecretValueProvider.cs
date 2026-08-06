using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker.Secrets;

public sealed class KeyVaultSecretValueProvider : ISecretValueProvider
{
    private readonly ConcurrentDictionary<string, SecretClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> GetSecretAsync(SecretLocatorV1 locator, CancellationToken cancellationToken)
    {
        var client = _clients.GetOrAdd(locator.VaultUri.AbsoluteUri,
            _ => new SecretClient(locator.VaultUri, new DefaultAzureCredential()));
        var response = await client.GetSecretAsync(locator.SecretName, locator.SecretVersion, cancellationToken);
        return response.Value.Value;
    }
}
