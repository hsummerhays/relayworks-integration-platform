using System.Net.Http.Headers;
using System.Text;

namespace RelayWorks.Sync.Worker.Authentication;

public sealed class ApiKeyAuthenticator : IConnectorAuthenticator
{
    private readonly string _apiKey;
    private readonly string _headerName;

    public ApiKeyAuthenticator(string apiKey, string? headerName = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required.", nameof(apiKey));

        _apiKey = apiKey.Trim();
        _headerName = string.IsNullOrWhiteSpace(headerName) ? "X-Api-Key" : headerName.Trim();
    }

    public Task<AuthHeader?> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
        Task.FromResult<AuthHeader?>(new AuthHeader(_headerName, _apiKey));

    public Task<bool> ValidateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));
}

public sealed class BasicAuthenticator : IConnectorAuthenticator
{
    private readonly string _headerValue;

    public BasicAuthenticator(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));

        var raw = $"{username}:{password ?? string.Empty}";
        _headerValue = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))}";
    }

    public Task<AuthHeader?> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
        Task.FromResult<AuthHeader?>(new AuthHeader("Authorization", _headerValue));

    public Task<bool> ValidateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(_headerValue));
}
