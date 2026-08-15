using System.Text.Json.Serialization;

namespace RelayWorks.Sync.Worker.Authentication;

public sealed record ApiKeyCredential(
    [property: JsonPropertyName("apiKey")] string? ApiKey,
    [property: JsonPropertyName("headerName")] string? HeaderName = "X-Api-Key");

public sealed record BasicAuthCredential(
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("password")] string? Password);

public sealed record OAuth2ClientCredential(
    [property: JsonPropertyName("tokenEndpoint")] string? TokenEndpoint,
    [property: JsonPropertyName("clientId")] string? ClientId,
    [property: JsonPropertyName("clientSecret")] string? ClientSecret,
    [property: JsonPropertyName("scope")] string? Scope);

public sealed record MutualTlsCredential(
    [property: JsonPropertyName("certificateBase64")] string? CertificateBase64,
    [property: JsonPropertyName("certificatePassword")] string? CertificatePassword);
