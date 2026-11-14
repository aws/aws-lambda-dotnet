using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>
/// Credentials from a <see cref="StaticRegistryCredentialSource"/> must only ever be sent to the host they were
/// created for, whatever host a request or a Bearer challenge points at.
/// </summary>
public class CredentialBindingTests
{
    private const string BoundHost = "111111111111.dkr.ecr.us-west-2.amazonaws.com";
    private static readonly BasicRegistryCredentials Creds = new("AWS", "bound-secret");

    private static readonly string BoundBasicHeader =
        "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("AWS:bound-secret"));

    /// <summary>Records the authority and Authorization header of every request it receives.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<(string Authority, string? Authorization)> Seen { get; } = new();

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request.RequestUri!.Authority, request.Headers.Authorization?.ToString()));
            return Task.FromResult(_responder(request));
        }
    }

    private static HttpResponseMessage Challenge(string scheme, string parameter)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(scheme, parameter));
        return response;
    }

    private static HttpClient CreateClient(RecordingHandler stub)
        => new(new AuthHandshakeHandler(new StaticRegistryCredentialSource(BoundHost, Creds.Username, Creds.Password), stub));

    [Fact]
    public async Task Static_source_returns_credentials_only_for_its_own_host()
    {
        var source = new StaticRegistryCredentialSource(BoundHost, Creds.Username, Creds.Password);

        Assert.Equal(Creds, await source.GetCredentialsAsync(BoundHost));
        Assert.Equal(Creds, await source.GetCredentialsAsync(BoundHost.ToUpperInvariant()));

        Assert.Null(await source.GetCredentialsAsync("attacker.example"));
        Assert.Null(await source.GetCredentialsAsync("222222222222.dkr.ecr.us-west-2.amazonaws.com"));
        Assert.Null(await source.GetCredentialsAsync(BoundHost + ":8443"));
        Assert.Null(await source.GetCredentialsAsync(BoundHost + ".attacker.example"));
    }

    [Theory]
    [InlineData("https://attacker.example/v2/")]
    [InlineData("https://" + BoundHost + ":8443/v2/")]
    public async Task Basic_challenge_from_any_other_host_gets_no_credentials(string url)
    {
        var stub = new RecordingHandler(_ => Challenge("Basic", "realm=\"registry\""));
        using HttpClient http = CreateClient(stub);

        await Assert.ThrowsAsync<RegistryAuthenticationException>(() => http.GetAsync(url));

        Assert.NotEmpty(stub.Seen);
        Assert.All(stub.Seen, seen => Assert.Null(seen.Authorization));
    }

    [Fact]
    public async Task Bearer_realm_on_another_host_gets_no_credentials()
    {
        // The bound host itself answers with a Bearer challenge whose token endpoint is some other host.
        var stub = new RecordingHandler(request => request.RequestUri!.Host == BoundHost
            ? Challenge("Bearer", "realm=\"https://attacker.example/token\",service=\"registry\"")
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using HttpClient http = CreateClient(stub);

        await Assert.ThrowsAsync<RegistryAuthenticationException>(() => http.GetAsync($"https://{BoundHost}/v2/"));

        (string Authority, string? Authorization) tokenRequest = Assert.Single(stub.Seen, s => s.Authority == "attacker.example");
        Assert.Null(tokenRequest.Authorization);
        Assert.DoesNotContain(stub.Seen, s => s.Authorization == BoundBasicHeader);
    }

    [Fact]
    public async Task Bearer_realm_on_the_bound_host_still_gets_credentials()
    {
        var stub = new RecordingHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/token")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"token\":\"t0k3n\"}") };
            }
            return request.Headers.Authorization is null
                ? Challenge("Bearer", $"realm=\"https://{BoundHost}/token\"")
                : new HttpResponseMessage(HttpStatusCode.OK);
        });
        using HttpClient http = CreateClient(stub);

        using HttpResponseMessage response = await http.GetAsync($"https://{BoundHost}/v2/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(stub.Seen, s => s.Authorization == BoundBasicHeader);
        Assert.Contains(stub.Seen, s => s.Authorization == "Bearer t0k3n");
    }
}
