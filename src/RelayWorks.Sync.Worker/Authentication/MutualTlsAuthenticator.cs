using System.Security.Cryptography.X509Certificates;

namespace RelayWorks.Sync.Worker.Authentication;

public sealed class MutualTlsAuthenticator : IConnectorAuthenticator
{
    private readonly MutualTlsCredential _credential;

    public MutualTlsAuthenticator(MutualTlsCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (string.IsNullOrWhiteSpace(credential.CertificateBase64))
            throw new ArgumentException("CertificateBase64 is required for mTLS.", nameof(credential));

        _credential = credential;
    }

    public Task<AuthHeader?> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
        Task.FromResult<AuthHeader?>(null); // mTLS is transport-level; no authorization header required

    public Task<bool> ValidateAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cert = LoadCertificate();
            return Task.FromResult(cert.HasPrivateKey);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public void ConfigureClient(HttpClient client, HttpMessageHandler? handler = null)
    {
        if (handler is HttpClientHandler clientHandler)
        {
            var cert = LoadCertificate();
            clientHandler.ClientCertificates.Add(cert);
            clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
        }
        else if (handler is SocketsHttpHandler socketsHandler)
        {
            var cert = LoadCertificate();
            socketsHandler.SslOptions.ClientCertificates ??= new X509CertificateCollection();
            socketsHandler.SslOptions.ClientCertificates.Add(cert);
        }
    }

    private X509Certificate2 LoadCertificate()
    {
        var rawBytes = Convert.FromBase64String(_credential.CertificateBase64!);
        return string.IsNullOrWhiteSpace(_credential.CertificatePassword)
            ? X509CertificateLoader.LoadCertificate(rawBytes)
            : X509CertificateLoader.LoadPkcs12(rawBytes, _credential.CertificatePassword);
    }
}
