using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using RelayWorks.Contracts.IntegrationRuns;
using RelayWorks.Sync.Worker.Secrets;

namespace RelayWorks.Sync.Worker.Authentication;

public interface IConnectorAuthenticatorFactory
{
    Task<IConnectorAuthenticator> CreateAuthenticatorAsync(
        ConnectorExecutionProfileV1 profile,
        CancellationToken cancellationToken);
}

public sealed class ConnectorAuthenticatorFactory : IConnectorAuthenticatorFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CachedSecretResolver _secretResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILoggerFactory _loggerFactory;

    public ConnectorAuthenticatorFactory(
        CachedSecretResolver secretResolver,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        _secretResolver = secretResolver;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _timeProvider = timeProvider;
        _loggerFactory = loggerFactory;
    }

    public async Task<IConnectorAuthenticator> CreateAuthenticatorAsync(
        ConnectorExecutionProfileV1 profile,
        CancellationToken cancellationToken)
    {
        var secretDocument = await _secretResolver.ResolveAsync(profile.Secret, cancellationToken);

        return profile.AuthType switch
        {
            ConnectorAuthenticationType.ApiKey => CreateApiKeyAuthenticator(secretDocument),
            ConnectorAuthenticationType.Basic => CreateBasicAuthenticator(secretDocument),
            ConnectorAuthenticationType.OAuth2 => CreateOAuth2Authenticator(secretDocument, profile),
            ConnectorAuthenticationType.MutualTls => CreateMutualTlsAuthenticator(secretDocument),
            _ => throw new NotSupportedException($"Authentication strategy '{profile.AuthType}' is not supported.")
        };
    }

    private static ApiKeyAuthenticator CreateApiKeyAuthenticator(string secretDocument)
    {
        // Secret may be a plain API key string or a JSON payload { "apiKey": "...", "headerName": "..." }
        if (TryDeserializeJson<ApiKeyCredential>(secretDocument, out var cred) && !string.IsNullOrWhiteSpace(cred?.ApiKey))
        {
            return new ApiKeyAuthenticator(cred.ApiKey, cred.HeaderName);
        }

        return new ApiKeyAuthenticator(secretDocument);
    }

    private static BasicAuthenticator CreateBasicAuthenticator(string secretDocument)
    {
        if (TryDeserializeJson<BasicAuthCredential>(secretDocument, out var cred) && !string.IsNullOrWhiteSpace(cred?.Username))
        {
            return new BasicAuthenticator(cred.Username, cred.Password ?? string.Empty);
        }

        // Support colon-separated username:password fallback
        var parts = secretDocument.Split(':', 2);
        if (parts.Length == 2)
        {
            return new BasicAuthenticator(parts[0], parts[1]);
        }

        throw new InvalidOperationException("Basic authentication secret must be a JSON object with username/password or formatted as 'username:password'.");
    }

    private OAuth2TokenAuthenticator CreateOAuth2Authenticator(string secretDocument, ConnectorExecutionProfileV1 profile)
    {
        if (!TryDeserializeJson<OAuth2ClientCredential>(secretDocument, out var cred) ||
            cred is null ||
            string.IsNullOrWhiteSpace(cred.TokenEndpoint) ||
            string.IsNullOrWhiteSpace(cred.ClientId) ||
            string.IsNullOrWhiteSpace(cred.ClientSecret))
        {
            throw new InvalidOperationException("OAuth2 secret must be a JSON document containing 'tokenEndpoint', 'clientId', and 'clientSecret'.");
        }

        var client = _httpClientFactory.CreateClient("RelayWorks.OAuth2");
        var logger = _loggerFactory.CreateLogger<OAuth2TokenAuthenticator>();
        var scopeKey = $"{profile.Secret.RoutingKey ?? "global"}:{profile.ConfigurationVersion}";
        return new OAuth2TokenAuthenticator(cred, scopeKey, client, _cache, _timeProvider, logger);
    }

    private static MutualTlsAuthenticator CreateMutualTlsAuthenticator(string secretDocument)
    {
        if (!TryDeserializeJson<MutualTlsCredential>(secretDocument, out var cred) ||
            cred is null ||
            string.IsNullOrWhiteSpace(cred.CertificateBase64))
        {
            throw new InvalidOperationException("MutualTls secret must be a JSON document containing 'certificateBase64'.");
        }

        return new MutualTlsAuthenticator(cred);
    }

    private static bool TryDeserializeJson<T>(string input, out T? result) where T : class
    {
        try
        {
            if (input.TrimStart().StartsWith('{'))
            {
                result = JsonSerializer.Deserialize<T>(input, JsonOptions);
                return result is not null;
            }
        }
        catch
        {
            // Ignore parse failures and fall back to alternative parsing
        }

        result = null;
        return false;
    }
}
