using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AWS.OCI.Builder;
using AWS.OCI.Builder.Images;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>How registry responses map to existence results and exceptions.</summary>
public class RegistryErrorMappingTests
{
    private const string EcrHost = "123456789012.dkr.ecr.us-west-2.amazonaws.com";
    private static readonly string AnyDigest = "sha256:" + new string('a', 64);

    [Theory]
    [InlineData(HttpStatusCode.OK, "true")]
    [InlineData(HttpStatusCode.NotFound, "false")]
    [InlineData(HttpStatusCode.TemporaryRedirect, "true")]   // a registry may redirect a blob HEAD to its storage
    [InlineData(HttpStatusCode.Unauthorized, nameof(RegistryAuthenticationException))]
    [InlineData(HttpStatusCode.Forbidden, nameof(RegistryAuthenticationException))]
    [InlineData(HttpStatusCode.TooManyRequests, nameof(RegistryRequestException))]
    [InlineData(HttpStatusCode.InternalServerError, nameof(RegistryRequestException))]
    public async Task BlobExists_maps_status(HttpStatusCode status, string expected)
    {
        var client = new RegistryClient(EcrHost, new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(status))));
        string actual;
        try { actual = (await client.BlobExistsAsync("repo", AnyDigest)) ? "true" : "false"; }
        catch (RegistryAuthenticationException ex) { actual = ex.GetType().Name; Assert.Equal((int)status, ex.StatusCode); }
        catch (RegistryException ex) { actual = ex.GetType().Name; }
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Ecr_DENIED_is_an_authentication_error()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"errors":[{"code":"DENIED","message":"Your authorization token has expired. Reauthenticate and try again."}]}"""),
        });
        var client = new RegistryClient(EcrHost, new HttpClient(stub));
        var ex = await Assert.ThrowsAsync<RegistryAuthenticationException>(
            () => client.PutManifestAsync("repo", "latest", "{}", MediaTypes.OciManifestV1));
        Assert.Contains("GetAuthorizationToken", ex.Message);
        Assert.Contains("expired", ex.Message);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Ecr_bare_400_on_HEAD_is_reported_as_an_authentication_error()
    {
        // What ECR does with an invalid token: HEAD -> 400 with no body; GET -> 400 with a DENIED body.
        var stub = new StubHttpHandler(r => r.Method == HttpMethod.Head
            ? new HttpResponseMessage(HttpStatusCode.BadRequest)
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"errors":[{"code":"DENIED","message":"Your Authorization Token is invalid."}]}"""),
            });
        var client = new RegistryClient(EcrHost, new HttpClient(stub));
        var ex = await Assert.ThrowsAsync<RegistryAuthenticationException>(() => client.BlobExistsAsync("repo", AnyDigest));
        Assert.Contains("Authorization Token is invalid", ex.Message);
        Assert.Equal(new[] { HttpMethod.Head, HttpMethod.Get }, stub.Requests.Select(r => r.Method));
    }

    [Fact]
    public async Task Bare_400_on_HEAD_is_reported_as_is_when_the_probe_fails_for_another_reason()
    {
        var stub = new StubHttpHandler(r => new HttpResponseMessage(r.Method == HttpMethod.Head ? HttpStatusCode.BadRequest : HttpStatusCode.ServiceUnavailable));
        var client = new RegistryClient(EcrHost, new HttpClient(stub));
        var ex = await Assert.ThrowsAsync<RegistryRequestException>(() => client.BlobExistsAsync("repo", AnyDigest));
        Assert.Equal(400, ex.StatusCode); // the HEAD's status, not the probe's 503
    }

    [Fact]
    public async Task Anonymous_refusal_suggests_a_missing_repository_not_credentials()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"errors":[{"code":"UNAUTHORIZED","message":"authentication required"}]}"""),
        });
        var anonymous = new RegistryClient("registry-1.docker.io", new HttpClient(stub), anonymous: true);
        var ex = await Assert.ThrowsAsync<RegistryAuthenticationException>(() => anonymous.GetManifestAsync("library/ubuntuu", "22.04"));
        Assert.Contains("may not exist", ex.Message);
        Assert.DoesNotContain("GetAuthorizationToken", ex.Message);

        var authenticated = new RegistryClient(EcrHost, new HttpClient(stub), anonymous: false);
        ex = await Assert.ThrowsAsync<RegistryAuthenticationException>(() => authenticated.GetManifestAsync("repo", "latest"));
        Assert.Contains("GetAuthorizationToken", ex.Message);
    }

    [Fact]
    public async Task Bare_400_on_HEAD_stays_a_request_error_when_the_registry_is_reachable()
    {
        var stub = new StubHttpHandler(r => new HttpResponseMessage(r.Method == HttpMethod.Head ? HttpStatusCode.BadRequest : HttpStatusCode.OK));
        var client = new RegistryClient(EcrHost, new HttpClient(stub));
        var ex = await Assert.ThrowsAsync<RegistryRequestException>(() => client.BlobExistsAsync("repo", AnyDigest));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Push_with_a_bad_token_fails_before_downloading_base_layers()
    {
        byte[] baseLayer = new byte[1024 * 1024];
        string baseDigest = RegistryClient.ComputeDigest(baseLayer);
        var source = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(baseLayer) });
        var target = new StubHttpHandler(_ => TestHttp.BasicChallenge(EcrHost)); // ECR keeps refusing the token

        const string configJson = """{"architecture":"amd64","os":"linux","rootfs":{"type":"layers","diff_ids":["sha256:aaaa"]},"config":{}}""";
        string manifestJson = $$"""{"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json","config":{"mediaType":"application/vnd.oci.image.config.v1+json","size":10,"digest":"{{AnyDigest}}"},"layers":[{"mediaType":"application/vnd.oci.image.layer.v1.tar+gzip","size":{{baseLayer.Length}},"digest":"{{baseDigest}}"}]}""";
        ImageBuilder imageBuilder = ImageBuilder.FromBaseImage(configJson, manifestJson, MediaTypes.OciManifestV1);
        var layers = new[] { new Descriptor { MediaType = MediaTypes.OciLayerGzipV1, Size = baseLayer.Length, Digest = baseDigest } };

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(
            ImageReference.Parse("public.ecr.aws/docker/library/alpine:3.20"),
            new RegistryClient("public.ecr.aws", new HttpClient(source)), imageBuilder, layers);
        builder.UseTargetClientFactoryForTest((host, creds) => new RegistryClient(host, new HttpClient(new AuthHandshakeHandler(creds, target))));

        string token = Convert.ToBase64String(Encoding.UTF8.GetBytes("AWS:expired"));
        var ex = await Assert.ThrowsAsync<RegistryAuthenticationException>(() => builder.PushToEcrAsync($"{EcrHost}/repo:tag", token));
        Assert.Equal(EcrHost, ex.RegistryName);
        Assert.Empty(source.Requests);
    }
}
