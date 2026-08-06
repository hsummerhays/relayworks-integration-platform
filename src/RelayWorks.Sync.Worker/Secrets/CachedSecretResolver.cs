using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Memory;
using RelayWorks.Contracts.IntegrationRuns;

namespace RelayWorks.Sync.Worker.Secrets;

public sealed class CachedSecretResolver(
    IMemoryCache cache,
    ISecretValueProvider provider,
    ISecretLocatorRouter router,
    TimeProvider timeProvider,
    ILogger<CachedSecretResolver> logger)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _failureCooldowns = new(StringComparer.Ordinal);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(10);
    private static readonly Meter Meter = new("RelayWorks.Sync.Worker.Secrets");
    private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("relayworks.secret.cache.hits");
    private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("relayworks.secret.cache.misses");
    private static readonly Counter<long> RefreshFailures = Meter.CreateCounter<long>("relayworks.secret.refresh.failures");

    public async Task<string> ResolveAsync(SecretLocatorV1 locator, CancellationToken cancellationToken)
    {
        locator = router.Route(locator);
        var key = $"{locator.VaultUri.AbsoluteUri}|{locator.SecretName}|{locator.SecretVersion ?? "latest"}";
        if (cache.TryGetValue<string>(key, out var cached))
        {
            CacheHits.Add(1);
            logger.LogDebug("Connector secret cache hit for {VaultHost}/{SecretName}", locator.VaultUri.Host, locator.SecretName);
            return cached!;
        }

        if (_failureCooldowns.TryGetValue(key, out var retryAfter) && retryAfter > timeProvider.GetUtcNow())
            throw new InvalidOperationException("Secret resolution is temporarily suppressed after a recent provider failure.");

        CacheMisses.Add(1);

        var lazy = _inflight.GetOrAdd(key, _ => new Lazy<Task<string>>(
            () => provider.GetSecretAsync(locator, CancellationToken.None),
            LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            var value = await lazy.Value.WaitAsync(cancellationToken);
            cache.Set(key, value, timeProvider.GetUtcNow().Add(CacheTtl));
            logger.LogInformation("Refreshed connector secret cache for {VaultHost}/{SecretName}",
                locator.VaultUri.Host, locator.SecretName);
            _failureCooldowns.TryRemove(key, out _);
            return value;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RefreshFailures.Add(1);
            _failureCooldowns[key] = timeProvider.GetUtcNow().Add(FailureCooldown);
            logger.LogWarning(exception, "Connector secret refresh failed for {VaultHost}/{SecretName}",
                locator.VaultUri.Host, locator.SecretName);
            throw;
        }
        finally { _inflight.TryRemove(key, out _); }
    }
}
