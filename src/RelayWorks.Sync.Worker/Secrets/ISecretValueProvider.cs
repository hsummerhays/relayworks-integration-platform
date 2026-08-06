using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker.Secrets;

public interface ISecretValueProvider
{
    Task<string> GetSecretAsync(SecretLocatorV1 locator, CancellationToken cancellationToken);
}
