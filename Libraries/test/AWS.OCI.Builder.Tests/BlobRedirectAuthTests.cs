using System.Net;
using System.Net.Http.Headers;
using Amazon.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>
/// Verifies that following a blob-download redirect to a different host never presents registry credentials to
/// the redirect target, even when that target answers with a 401 authentication challenge.
/// </summary>
public class BlobRedirectAuthTests
{
    private const string RegistryHost = "registry.example.com";
    private const string Digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    private sealed class StaticCredentials : IRegistryCredentialSource
    {
        public Task<BasicRegistryCredentials?> GetCredentialsAsync(string registryHost, CancellationToken ct = default)
            => Task.FromResult<BasicRegistryCredentials?>(new BasicRegistryCredentials("AWS", "secret"));
    }

    /// <summary>Records every request and delegates the response to a callback.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<(Uri Uri, AuthenticationHeaderValue? Auth)> Requests { get; } = new();

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, request.Headers.Authorization));
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage RedirectTo(string location)
    {
        var redirect = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
        redirect.Headers.Location = new Uri(location);
        return redirect;
    }

    [Fact]
    public async Task Cross_host_redirect_target_challenge_does_not_receive_credentials()
    {
        var stub = new RecordingHandler(request =>
        {
            Uri uri = request.RequestUri!;
            if (uri.Host == RegistryHost)
            {
                // The registry redirects the blob download to a host it controls.
                return RedirectTo("https://attacker.example.com/blob");
            }

            // The redirect target tries to harvest credentials with a challenge (Bearer realm on its own host).
            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", "realm=\"https://attacker.example.com/token\",service=\"attacker\""));
            return unauthorized;
        });

        var client = new RegistryClient(
            RegistryHost, new HttpClient(new AuthHandshakeHandler(new StaticCredentials(), stub)));

        await Assert.ThrowsAnyAsync<RegistryException>(() => client.GetBlobAsync("app", Digest));

        // The attacker host was contacted exactly once (the redirect hop), with no Authorization header, and its
        // token endpoint was never called.
        var attackerRequests = stub.Requests.Where(r => r.Uri.Host == "attacker.example.com").ToList();
        Assert.Single(attackerRequests);
        Assert.Null(attackerRequests[0].Auth);
        Assert.DoesNotContain(stub.Requests, r => r.Uri.AbsolutePath == "/token");
    }

    [Fact]
    public async Task Cross_host_redirect_to_storage_succeeds_without_credentials()
    {
        byte[] payload = "layer-bytes"u8.ToArray();
        var stub = new RecordingHandler(request =>
            request.RequestUri!.Host == RegistryHost
                ? RedirectTo("https://storage.example.com/presigned?sig=abc")
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });

        var client = new RegistryClient(
            RegistryHost, new HttpClient(new AuthHandshakeHandler(new StaticCredentials(), stub)));

        await using (Stream stream = await client.GetBlobAsync("app", Digest))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Assert.Equal(payload, buffer.ToArray());
        }

        var storageRequest = Assert.Single(stub.Requests, r => r.Uri.Host == "storage.example.com");
        Assert.Null(storageRequest.Auth);
    }
}
