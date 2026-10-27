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

    private static readonly HttpRequestOptionsKey<string> MarkerOption = new("test-marker");

    [Theory]
    // ECR / Docker Hub style (no whitespace).
    [InlineData("realm=\"https://a/token\",service=\"reg\",scope=\"repository:app:pull\"", "https://a/token", "reg", "repository:app:pull")]
    // Optional whitespace after commas.
    [InlineData("realm=\"https://a/token\", service=\"reg\", scope=\"repository:app:pull\"", "https://a/token", "reg", "repository:app:pull")]
    // Optional whitespace BEFORE commas — the old regex silently dropped realm here.
    [InlineData("realm=\"https://a/token\" , service=\"reg\"", "https://a/token", "reg", null)]
    // Whitespace around '=' and tabs.
    [InlineData("realm = \"https://a/token\",\tservice\t=\t\"reg\"", "https://a/token", "reg", null)]
    // Unquoted token values — the old regex parsed nothing.
    [InlineData("realm=https://a/token,service=reg", "https://a/token", "reg", null)]
    // A comma inside a quoted value stays part of the value.
    [InlineData("realm=\"https://a/token\",scope=\"repository:app:pull,push\"", "https://a/token", null, "repository:app:pull,push")]
    public void Parses_bearer_challenge_parameters(string parameters, string realm, string? service, string? scope)
    {
        Dictionary<string, string> parsed = AuthHandshakeHandler.ParseBearerParameters(parameters);

        Assert.Equal(realm, parsed["realm"]);
        Assert.Equal(service, parsed.GetValueOrDefault("service"));
        Assert.Equal(scope, parsed.GetValueOrDefault("scope"));
    }

    [Fact]
    public void Parses_hyphenated_names_escaped_quotes_and_ignores_malformed_segments()
    {
        Dictionary<string, string> parsed = AuthHandshakeHandler.ParseBearerParameters(
            "realm=\"https://a/token\", garbage, x-custom=\"1\", error_description=\"say \\\"hi\\\"\", REALM=\"second\"");

        Assert.Equal("https://a/token", parsed["realm"]);        // first occurrence wins; names are case-insensitive
        Assert.Equal("1", parsed["x-custom"]);                    // hyphen is a valid token character
        Assert.Equal("say \"hi\"", parsed["error_description"]);  // backslash escapes are unescaped
        Assert.False(parsed.ContainsKey("garbage"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parses_empty_parameters_as_empty(string? parameters)
        => Assert.Empty(AuthHandshakeHandler.ParseBearerParameters(parameters));

    [Fact]
    public async Task Handshake_succeeds_with_whitespace_before_commas_in_challenge()
    {
        var stub = new StubHandler(request =>
        {
            if (request.RequestUri!.ToString().StartsWith("https://auth.example.com/token"))
            {
                Assert.Contains("service=registry.example.com", request.RequestUri.Query, StringComparison.Ordinal);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"abc123\",\"expires_in\":3600}"),
                };
            }

            if (request.Headers.Authorization?.Scheme == "Bearer")
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", "realm=\"https://auth.example.com/token\" , service=\"registry.example.com\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        using HttpResponseMessage response = await client.GetAsync("https://registry.example.com/v2/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, stub.Requests.Count);
    }

    [Fact]
    public async Task Handshake_retry_is_disposed_without_closing_the_callers_body_stream()
    {
        var stub = new StubHandler(request =>
        {
            if (request.RequestUri!.ToString().StartsWith("https://auth.example.com/token"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"abc123\",\"expires_in\":3600}"),
                };
            }

            if (request.Headers.Authorization?.Scheme == "Bearer")
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", "realm=\"https://auth.example.com/token\",service=\"registry.example.com\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        // A seekable body, as in a real layer upload (FileStream / MemoryStream) — owned by the caller.
        var body = new MemoryStream("layer-bytes"u8.ToArray());
        using var request = new HttpRequestMessage(HttpMethod.Patch, "https://registry.example.com/v2/app/blobs/uploads/1")
        {
            Content = new StreamContent(body),
        };
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // The retry clone was disposed: its content can no longer be read.
        HttpRequestMessage retry = stub.Requests[^1];
        Assert.NotSame(request, retry);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => retry.Content!.ReadAsByteArrayAsync());

        // ...but the caller's stream is still open and usable.
        Assert.True(body.CanRead);
        body.Position = 0;
        Assert.Equal("layer-bytes"u8.ToArray(), body.ToArray());
    }

    [Fact]
    public async Task Handshake_retry_preserves_version_policy_and_request_options()
    {
        var stub = new StubHandler(request =>
        {
            string url = request.RequestUri!.ToString();
            if (url.StartsWith("https://auth.example.com/token"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"abc123\",\"expires_in\":3600}"),
                };
            }

            if (request.Headers.Authorization?.Scheme == "Bearer")
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", "realm=\"https://auth.example.com/token\",service=\"registry.example.com\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://registry.example.com/v2/");
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        request.Options.Set(MarkerOption, "kept");
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The last request is the authenticated retry produced by CloneAsync.
        HttpRequestMessage retry = stub.Requests[^1];
        Assert.NotSame(request, retry);
        Assert.Equal(HttpVersionPolicy.RequestVersionExact, retry.VersionPolicy);
        Assert.True(retry.Options.TryGetValue(MarkerOption, out string? marker));
        Assert.Equal("kept", marker);
    }

    [Fact]
    public async Task Caller_supplied_authorization_is_not_overwritten_by_cached_auth()
    {
        var stub = new StubHandler(request =>
        {
            string url = request.RequestUri!.ToString();
            if (url.StartsWith("https://auth.example.com/token"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"abc123\",\"expires_in\":3600}"),
                };
            }

            if (request.Headers.Authorization?.Scheme == "Bearer")
            {
                // Echo back which token the registry received.
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.Headers.Authorization.Parameter!) };
            }

            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", "realm=\"https://auth.example.com/token\",service=\"registry.example.com\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        // 1. Prime the cache with a handshake-derived token for the host.
        using (HttpResponseMessage primed = await client.GetAsync("https://registry.example.com/v2/"))
        {
            Assert.Equal("abc123", await primed.Content.ReadAsStringAsync());
        }

        // 2. A request carrying the caller's own Authorization must reach the registry unchanged.
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://registry.example.com/v2/app/manifests/latest");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "caller-token");
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal("caller-token", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Caller_supplied_authorization_401_is_returned_without_a_handshake()
    {
        var stub = new StubHandler(_ =>
        {
            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", "realm=\"https://auth.example.com/token\",service=\"registry.example.com\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://registry.example.com/v2/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "caller-token");
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(stub.Requests); // no token request, no retry
    }

    [Fact]
    public async Task Requests_marked_skip_authentication_are_not_authenticated()
    {
        var stub = new StubHandler(_ =>
        {
            var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Basic", "realm=\"x\""));
            return unauthorized;
        });

        var handler = new AuthHandshakeHandler(
            new StaticCredentials(new BasicRegistryCredentials("AWS", "secret")), stub);
        using var client = new HttpClient(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://storage.example.com/blob");
        request.Options.Set(AuthHandshakeHandler.SkipAuthentication, true);
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(stub.Requests);
        Assert.Null(stub.Requests[0].Headers.Authorization);
    }
}
