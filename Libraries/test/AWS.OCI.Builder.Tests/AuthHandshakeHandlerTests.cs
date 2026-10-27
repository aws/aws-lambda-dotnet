using System.Net;
using System.Net.Http.Headers;
using Amazon.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class AuthHandshakeHandlerTests
{
    /// <summary>A programmable inner handler that responds based on the request URI.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class StaticCredentials : IRegistryCredentialSource
    {
        private readonly BasicRegistryCredentials? _creds;
        public StaticCredentials(BasicRegistryCredentials? creds) => _creds = creds;
        public Task<BasicRegistryCredentials?> GetCredentialsAsync(string registryHost, CancellationToken ct = default)
            => Task.FromResult(_creds);
    }

    [Fact]
    public async Task Performs_bearer_handshake_and_retries_with_token()
    {
        var stub = new StubHandler(request =>
        {
            string url = request.RequestUri!.ToString();

            // The token endpoint: return a token when Basic auth was presented.
            if (url.StartsWith("https://auth.example.com/token"))
            {
                Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"abc123\",\"expires_in\":3600}"),
                };
            }

            // The resource endpoint: 401 until a Bearer token is presented.
            if (request.Headers.Authorization?.Scheme == "Bearer")
            {
                Assert.Equal("abc123", request.Headers.Authorization.Parameter);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
            }

            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer",
                "realm=\"https://auth.example.com/token\",service=\"registry.example.com\",scope=\"repository:app:pull\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        using HttpResponseMessage response = await client.GetAsync("https://registry.example.com/v2/app/manifests/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());

        // Expect: initial 401, token GET, authenticated retry.
        Assert.Equal(3, stub.Requests.Count);
    }

    [Fact]
    public async Task Selects_bearer_challenge_when_it_is_not_the_first_challenge()
    {
        // A registry may return multiple WWW-Authenticate challenges in any order. The handler must scan all of
        // them and pick the usable Bearer challenge even when an unsupported scheme is listed first — the previous
        // implementation inspected only the first challenge and would have failed here.
        var stub = new StubHandler(request =>
        {
            string url = request.RequestUri!.ToString();

            if (url.StartsWith("https://auth.example.com/token"))
            {
                Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"abc123\",\"expires_in\":3600}"),
                };
            }

            if (request.Headers.Authorization?.Scheme == "Bearer")
            {
                Assert.Equal("abc123", request.Headers.Authorization.Parameter);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
            }

            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            // Unsupported scheme first, usable Bearer second.
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Negotiate"));
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer",
                "realm=\"https://auth.example.com/token\",service=\"registry.example.com\",scope=\"repository:app:pull\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        using HttpResponseMessage response = await client.GetAsync("https://registry.example.com/v2/app/manifests/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
        Assert.Equal(3, stub.Requests.Count);
    }

    [Fact]
    public async Task Falls_back_to_basic_when_no_usable_bearer_challenge_is_present()
    {
        // When only an unsupported scheme and a Basic challenge are offered, the handler should select Basic and
        // present Basic credentials directly (no token round-trip).
        var stub = new StubHandler(request =>
        {
            if (request.Headers.Authorization?.Scheme == "Basic")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
            }

            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Negotiate"));
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Basic", "realm=\"registry.example.com\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        using HttpResponseMessage response = await client.GetAsync("https://registry.example.com/v2/app/manifests/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
        // Initial 401 + authenticated (Basic) retry — no token endpoint call.
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task Rejects_bearer_realm_pointing_at_loopback()
    {
        var stub = new StubHandler(_ =>
        {
            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", "realm=\"https://127.0.0.1/token\",service=\"evil\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidAuthResponseException>(
            () => client.GetAsync("https://registry.example.com/v2/app/manifests/latest"));
    }

    [Fact]
    public async Task Passes_through_non_401_responses()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("direct") });
        var handler = new AuthHandshakeHandler(new AnonymousRegistryCredentialSource(), stub);
        using var client = new HttpClient(handler);

        using HttpResponseMessage response = await client.GetAsync("https://registry.example.com/v2/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(stub.Requests);
    }
}
