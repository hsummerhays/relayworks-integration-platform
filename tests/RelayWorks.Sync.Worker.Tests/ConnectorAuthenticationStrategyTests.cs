using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker.Authentication;
using RelayWorks.Sync.Worker.Secrets;

namespace RelayWorks.Sync.Worker.Tests;

public sealed class ConnectorAuthenticationStrategyTests
{
    [Fact]
    public async Task ApiKey_authenticator_formats_custom_configurable_header()
    {
        var ct = TestContext.Current.CancellationToken;
        var auth = new ApiKeyAuthenticator("secret-key-123", "X-Custom-Vendor-Key");
        var header = await auth.GetAuthorizationHeaderAsync(ct);

        Assert.NotNull(header);
        Assert.Equal("X-Custom-Vendor-Key", header.Name);
        Assert.Equal("secret-key-123", header.Value);
        Assert.True(await auth.ValidateAsync(ct));
    }

    [Fact]
    public async Task Basic_authenticator_encodes_base64_credentials()
    {
        var ct = TestContext.Current.CancellationToken;
        var auth = new BasicAuthenticator("admin", "pass123");
        var header = await auth.GetAuthorizationHeaderAsync(ct);

        Assert.NotNull(header);
        Assert.Equal("Authorization", header.Name);
        Assert.Equal("Basic YWRtaW46cGFzczEyMw==", header.Value);
        Assert.True(await auth.ValidateAsync(ct));
    }

    [Fact]
    public async Task OAuth2_authenticator_isolates_tokens_by_tenant_and_configuration_identity()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockHttp = new MockHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = $"jwt-token-{Guid.NewGuid():N}",
                    expires_in = 3600,
                    token_type = "Bearer"
                }))
            }));

        using var httpClient = new HttpClient(mockHttp);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cred = new OAuth2ClientCredential("https://login.microsoftonline.com/oauth2/v2.0/token", "client-id-1", "client-secret-1", "api://default");

        var authTenantA = new OAuth2TokenAuthenticator(cred, "tenant-a:v1", httpClient, cache, TimeProvider.System, NullLogger<OAuth2TokenAuthenticator>.Instance);
        var authTenantB = new OAuth2TokenAuthenticator(cred, "tenant-b:v1", httpClient, cache, TimeProvider.System, NullLogger<OAuth2TokenAuthenticator>.Instance);

        var tokenA = await authTenantA.AcquireTokenAsync(ct);
        var tokenB = await authTenantB.AcquireTokenAsync(ct);

        // Different tenant scopes must trigger separate token acquisitions and separate cache entries
        Assert.NotEqual(tokenA, tokenB);
        Assert.Equal(2, mockHttp.RequestCount);
    }

    [Fact]
    public async Task OAuth2_authenticator_coalesces_concurrent_token_requests_within_same_scope()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockHttp = new MockHttpMessageHandler(async request =>
        {
            await Task.Delay(20, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    access_token = "jwt-access-token-999",
                    expires_in = 3600,
                    token_type = "Bearer"
                }))
            };
        });

        using var httpClient = new HttpClient(mockHttp);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cred = new OAuth2ClientCredential("https://login.microsoftonline.com/oauth2/v2.0/token", "client-id-1", "client-secret-1", "api://default");
        var auth = new OAuth2TokenAuthenticator(cred, "tenant-a:v1", httpClient, cache, TimeProvider.System, NullLogger<OAuth2TokenAuthenticator>.Instance);

        // Run 50 concurrent token acquisitions simultaneously
        var tokens = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => auth.GetAuthorizationHeaderAsync(ct)));

        Assert.All(tokens, t =>
        {
            Assert.NotNull(t);
            Assert.Equal("Authorization", t.Name);
            Assert.Equal("Bearer jwt-access-token-999", t.Value);
        });

        // Proves concurrent refresh coalescing and caching
        Assert.Equal(1, mockHttp.RequestCount);
    }

    [Fact]
    public async Task OAuth2_authenticator_sanitizes_failure_and_does_not_leak_secret()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockHttp = new MockHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("Invalid client credentials")
            }));

        using var httpClient = new HttpClient(mockHttp);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cred = new OAuth2ClientCredential("https://login.microsoftonline.com/oauth2/v2.0/token", "client-id-1", "super-secret-client-password", null);
        var auth = new OAuth2TokenAuthenticator(cred, "tenant-a:v1", httpClient, cache, TimeProvider.System, NullLogger<OAuth2TokenAuthenticator>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => auth.AcquireTokenAsync(ct));

        Assert.Contains("HTTP 401", ex.Message);
        Assert.DoesNotContain("super-secret-client-password", ex.Message);
    }

    [Fact]
    public async Task Factory_resolves_and_creates_correct_strategy_from_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        var cred = new OAuth2ClientCredential("https://login.microsoftonline.com/oauth2/v2.0/token", "client-id-1", "client-secret-1", null);
        var secretJson = JsonSerializer.Serialize(cred);

        var secretResolver = new CachedSecretResolver(
            new MemoryCache(new MemoryCacheOptions()),
            new StaticSecretProvider(secretJson),
            new PassThroughRouter(),
            TimeProvider.System,
            NullLogger<CachedSecretResolver>.Instance);

        var mockHttp = new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { access_token = "token-1", expires_in = 3600 }))
        }));

        var factory = new ConnectorAuthenticatorFactory(
            secretResolver,
            new SingleHttpClientFactory(new HttpClient(mockHttp)),
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            NullLoggerFactory.Instance);

        var profile = new ConnectorExecutionProfileV1(
            "SimulatedAccounting", true, true, 2, "v1",
            new SecretLocatorV1(new Uri("https://vault.azure.net"), "oauth-secret", RoutingKey: "tenant-a"),
            ConnectorAuthenticationType.OAuth2);

        var authenticator = await factory.CreateAuthenticatorAsync(profile, ct);
        Assert.IsType<OAuth2TokenAuthenticator>(authenticator);

        var header = await authenticator.GetAuthorizationHeaderAsync(ct);
        Assert.Equal("Bearer token-1", header?.Value);
    }

    private sealed class StaticSecretProvider(string secret) : ISecretValueProvider
    {
        public Task<string> GetSecretAsync(SecretLocatorV1 locator, CancellationToken cancellationToken) =>
            Task.FromResult(secret);
    }

    private sealed class PassThroughRouter : ISecretLocatorRouter
    {
        public SecretLocatorV1 Route(SecretLocatorV1 locator) => locator;
    }

    private sealed class SingleHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        private int _count;
        public int RequestCount => Volatile.Read(ref _count);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            return handler(request);
        }
    }
}
