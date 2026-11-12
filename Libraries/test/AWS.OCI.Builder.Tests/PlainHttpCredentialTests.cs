using System.Net;
using System.Text;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>
/// Registry credentials must never be sent over plain http. The handler's cache is keyed by host and port, which
/// ignores the scheme, so an http URL on the registry's host must not pick up the cached https header, and an
/// upload location the registry hands back must not move the upload onto http.
/// </summary>
public class PlainHttpCredentialTests
{
    private const string EcrHost = "111111111111.dkr.ecr.us-west-2.amazonaws.com";
    private const string Digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    private static AuthHandshakeHandler Handler(HttpMessageHandler inner)
        => new(new StaticRegistryCredentialSource(EcrHost, "AWS", "ecr-secret"), inner);

    /// <summary>Answers https requests without credentials with a Basic challenge, and everything else with 200.</summary>
    private static HttpResponseMessage ChallengeHttpsOnly(HttpRequestMessage request)
        => request.RequestUri!.Scheme == Uri.UriSchemeHttps && request.Headers.Authorization is null
            ? TestHttp.BasicChallenge(EcrHost)
            : new HttpResponseMessage(HttpStatusCode.OK);

    [Fact]
    public async Task Cached_credentials_are_not_attached_to_an_http_request_on_the_same_host()
    {
        var stub = new StubHttpHandler(ChallengeHttpsOnly);
        using var http = new HttpClient(Handler(stub));

        // Populate the cache over https: challenge, then a retry with Basic credentials.
        using (HttpResponseMessage ok = await http.GetAsync($"https://{EcrHost}/v2/"))
        {
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        Assert.Equal("Basic", stub.Requests[^1].Auth);

        // Same host, plain http: http://host and https://host share the cache key, so this is the case to guard.
        using HttpResponseMessage plain = await http.GetAsync($"http://{EcrHost}/v2/repo/blobs/uploads/1");

        Assert.Equal(Uri.UriSchemeHttp, stub.Requests[^1].Uri.Scheme);
        Assert.Null(stub.Requests[^1].Auth);
    }

    [Fact]
    public async Task A_challenge_over_http_is_not_answered()
    {
        // The host answers every request without credentials with a Basic challenge, over either scheme.
        var stub = new StubHttpHandler(r => r.Headers.Authorization is null
            ? TestHttp.BasicChallenge(EcrHost)
            : new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(Handler(stub));

        using HttpResponseMessage response = await http.GetAsync($"http://{EcrHost}/v2/");

        // The 401 goes back to the caller unanswered, and no request carried credentials.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(stub.Requests);
        Assert.Null(stub.Requests[0].Auth);
    }

    [Fact]
    public async Task Upload_start_with_an_http_location_is_rejected_before_any_bytes_are_sent()
    {
        var stub = new StubHttpHandler(r => r.Method == HttpMethod.Head
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Accepted($"http://{EcrHost}/v2/repo/blobs/uploads/abc?_state=session-secret"));
        var client = new RegistryClient(EcrHost, new HttpClient(Handler(stub)));

        var ex = await Assert.ThrowsAsync<RegistryRequestException>(
            () => client.UploadBlobAsync("repo", Digest, new MemoryStream(Encoding.ASCII.GetBytes("layer"))));

        Assert.Contains("non-https upload location", ex.Message);
        Assert.DoesNotContain("session-secret", ex.Message); // the upload URL's query is never quoted
        Assert.DoesNotContain(stub.Requests, r => r.Method == HttpMethod.Patch || r.Method == HttpMethod.Put);
        Assert.DoesNotContain(stub.Requests, r => r.Uri.Scheme == Uri.UriSchemeHttp);
    }

    [Fact]
    public async Task Patch_response_with_an_http_location_is_rejected_before_the_commit()
    {
        var stub = new StubHttpHandler(r =>
            r.Method == HttpMethod.Head ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : r.Method == HttpMethod.Post ? Accepted($"https://{EcrHost}/v2/repo/blobs/uploads/abc")
            : Accepted($"http://{EcrHost}/v2/repo/blobs/uploads/abc"));
        var client = new RegistryClient(EcrHost, new HttpClient(Handler(stub)));

        await Assert.ThrowsAsync<RegistryRequestException>(
            () => client.UploadBlobAsync("repo", Digest, new MemoryStream(Encoding.ASCII.GetBytes("layer"))));

        Assert.DoesNotContain(stub.Requests, r => r.Method == HttpMethod.Put);
        Assert.DoesNotContain(stub.Requests, r => r.Uri.Scheme == Uri.UriSchemeHttp);
    }

    [Fact]
    public async Task Relative_and_https_locations_still_upload()
    {
        var stub = new StubHttpHandler(r =>
            r.Method == HttpMethod.Head ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : r.Method == HttpMethod.Post ? Accepted("/v2/repo/blobs/uploads/abc")
            : r.Method == HttpMethod.Patch ? Accepted($"https://{EcrHost}/v2/repo/blobs/uploads/abc?_state=2")
            : new HttpResponseMessage(HttpStatusCode.Created));
        var client = new RegistryClient(EcrHost, new HttpClient(stub));

        await client.UploadBlobAsync("repo", Digest, new MemoryStream(Encoding.ASCII.GetBytes("layer")));

        Assert.Equal(HttpMethod.Put, stub.Requests[^1].Method);
        Assert.All(stub.Requests, r => Assert.Equal(Uri.UriSchemeHttps, r.Uri.Scheme));
    }

    private static HttpResponseMessage Accepted(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }
}
