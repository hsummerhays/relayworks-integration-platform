namespace RelayWorks.Sync.Worker.Authentication;

public sealed record AuthHeader(string Name, string Value);

public interface IConnectorAuthenticator
{
    Task<AuthHeader?> GetAuthorizationHeaderAsync(CancellationToken cancellationToken);
    Task<bool> ValidateAsync(CancellationToken cancellationToken);
    void ConfigureClient(HttpClient client, HttpMessageHandler? handler = null) { }
}
