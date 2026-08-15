using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace RelayWorks.Sync.Worker.Authentication;

public sealed partial class OAuth2TokenAuthenticator : IConnectorAuthenticator
{
    private readonly OAuth2ClientCredential _credential;
    private readonly string _scopeKey;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OAuth2TokenAuthenticator> _logger;

    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inflight = new(StringComparer.Ordinal);
    private static readonly TimeSpan BufferWindow = TimeSpan.FromMinutes(1);

    public OAuth2TokenAuthenticator(
        OAuth2ClientCredential credential,
        string scopeKey,
        HttpClient httpClient,
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILogger<OAuth2TokenAuthenticator> logger)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (string.IsNullOrWhiteSpace(credential.TokenEndpoint)) throw new ArgumentException("TokenEndpoint is required.");
        if (string.IsNullOrWhiteSpace(credential.ClientId)) throw new ArgumentException("ClientId is required.");
        if (string.IsNullOrWhiteSpace(credential.ClientSecret)) throw new ArgumentException("ClientSecret is required.");

        _credential = credential;
        _scopeKey = string.IsNullOrWhiteSpace(scopeKey) ? "global" : scopeKey.Trim();
        _httpClient = httpClient;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AuthHeader?> GetAuthorizationHeaderAsync(CancellationToken cancellationToken)
    {
        var token = await AcquireTokenAsync(cancellationToken);
        return new AuthHeader("Authorization", $"Bearer {token}");
    }

    public async Task<bool> ValidateAsync(CancellationToken cancellationToken)
    {
        var token = await AcquireTokenAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(token);
    }

    public async Task<string> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        var cacheKey = $"oauth2:{_scopeKey}|{_credential.TokenEndpoint}|{_credential.ClientId}|{_credential.Scope ?? string.Empty}";

        if (_cache.TryGetValue<string>(cacheKey, out var cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            LogTokenCacheHit(_logger, _credential.ClientId ?? "unknown");
            return cachedToken!;
        }

        var lazy = _inflight.GetOrAdd(cacheKey, _ => new Lazy<Task<string>>(
            () => RequestTokenFromEndpointAsync(cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var token = await lazy.Value;
            return token;
        }
        finally
        {
            _inflight.TryRemove(cacheKey, out _);
        }
    }

    private async Task<string> RequestTokenFromEndpointAsync(CancellationToken cancellationToken)
    {
        LogRequestingToken(_logger, _credential.ClientId ?? "unknown", _credential.TokenEndpoint ?? string.Empty);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _credential.ClientId ?? string.Empty,
            ["client_secret"] = _credential.ClientSecret ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(_credential.Scope))
        {
            form["scope"] = _credential.Scope!;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _credential.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sanitized failure logging: strictly avoid logging secret payload
            LogTokenRequestFailed(_logger, ex.GetType().Name, _credential.ClientId ?? "unknown");
            throw new InvalidOperationException($"OAuth2 token request failed for client '{_credential.ClientId}' due to network/transport error: {ex.GetType().Name}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            LogTokenRequestRejected(_logger, statusCode, _credential.ClientId ?? "unknown");
            throw new InvalidOperationException($"OAuth2 token request rejected with HTTP {statusCode} for client '{_credential.ClientId}'.");
        }

        var payload = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("OAuth2 token endpoint returned an empty or invalid access token.");
        }

        var expiresIn = TimeSpan.FromSeconds(payload.ExpiresIn > 0 ? payload.ExpiresIn : 3600);
        var effectiveTtl = expiresIn > BufferWindow ? expiresIn - BufferWindow : expiresIn;
        var expiration = _timeProvider.GetUtcNow().Add(effectiveTtl);

        var cacheKey = $"oauth2:{_scopeKey}|{_credential.TokenEndpoint}|{_credential.ClientId}|{_credential.Scope ?? string.Empty}";
        _cache.Set(cacheKey, payload.AccessToken, expiration);

        LogTokenRefreshed(_logger, _credential.ClientId ?? "unknown", (int)effectiveTtl.TotalSeconds);
        return payload.AccessToken;
    }

    private sealed record OAuthTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OAuth2 token cache hit for client {ClientId}")]
    private static partial void LogTokenCacheHit(ILogger logger, string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Requesting OAuth2 token for client {ClientId} from {Endpoint}")]
    private static partial void LogRequestingToken(ILogger logger, string clientId, string endpoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refreshed OAuth2 token for client {ClientId} (cached for {TtlSeconds}s)")]
    private static partial void LogTokenRefreshed(ILogger logger, string clientId, int ttlSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OAuth2 token request failed with {ErrorType} for client {ClientId}")]
    private static partial void LogTokenRequestFailed(ILogger logger, string errorType, string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OAuth2 token endpoint returned HTTP {StatusCode} for client {ClientId}")]
    private static partial void LogTokenRequestRejected(ILogger logger, int statusCode, string clientId);
}
