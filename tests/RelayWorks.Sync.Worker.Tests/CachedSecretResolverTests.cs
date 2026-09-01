using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker.Secrets;

namespace RelayWorks.Sync.Worker.Tests;

public sealed class CachedSecretResolverTests
{
    [Fact]
    public async Task Concurrent_record_volume_causes_one_vault_request()
    {
        var provider = new CountingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new CachedSecretResolver(cache, provider, new PassThroughRouter(), TimeProvider.System,
            NullLogger<CachedSecretResolver>.Instance);
        var locator = new SecretLocatorV1(new Uri("https://tenant.vault.azure.net"), "field-operations-payroll", "v1");

        await Task.WhenAll(Enumerable.Range(0, 5000).Select(_ => resolver.ResolveAsync(locator, default)));

        Assert.Equal(1, provider.Requests);
    }

    private sealed class PassThroughRouter : ISecretLocatorRouter
    {
        public SecretLocatorV1 Route(SecretLocatorV1 locator) => locator;
    }

    private sealed class CountingProvider : ISecretValueProvider
    {
        public int Requests;
        public async Task<string> GetSecretAsync(SecretLocatorV1 locator, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            await Task.Delay(10, cancellationToken);
            return "not-logged-secret";
        }
    }
}
