using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AWS.OCI.Builder;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>The base image digests recorded by <see cref="OciImageBuilder.FromBaseImageAsync(string, string, CancellationToken)"/>.</summary>
public class BaseImageDigestTests
{
    private const string Registry = "registry.example";

    private static readonly byte[] Config = Encoding.UTF8.GetBytes(
        """{"architecture":"arm64","os":"linux","rootfs":{"type":"layers","diff_ids":[]},"config":{}}""");
    private static readonly string ConfigDigest = RegistryClient.ComputeDigest(Config);

    private static readonly byte[] PlatformManifest = Encoding.UTF8.GetBytes(
        $$"""{"schemaVersion":2,"mediaType":"{{MediaTypes.OciManifestV1}}","config":{"mediaType":"{{MediaTypes.OciImageConfigV1}}","size":{{Config.Length}},"digest":"{{ConfigDigest}}"},"layers":[]}""");
    private static readonly string PlatformDigest = RegistryClient.ComputeDigest(PlatformManifest);

    private static readonly byte[] Index = Encoding.UTF8.GetBytes(
        $$$"""{"schemaVersion":2,"mediaType":"{{{MediaTypes.OciImageIndexV1}}}","manifests":[{"mediaType":"{{{MediaTypes.OciManifestV1}}}","size":{{{PlatformManifest.Length}}},"digest":"{{{PlatformDigest}}}","platform":{"os":"linux","architecture":"arm64"}}]}""");

    private static HttpResponseMessage Serve(HttpRequestMessage request)
    {
        string path = request.RequestUri!.AbsolutePath;
        (byte[]? body, string? mediaType) = path switch
        {
            "/v2/base/manifests/1.0" => (Index, MediaTypes.OciImageIndexV1),
            _ when path == $"/v2/base/manifests/{PlatformDigest}" => (PlatformManifest, MediaTypes.OciManifestV1),
            _ when path == $"/v2/base/blobs/{ConfigDigest}" => (Config, "application/octet-stream"),
            _ => (null, null),
        };
        if (body is null)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType!);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static Task<OciImageBuilder> PullAsync(string reference)
        => OciImageBuilder.FromBaseImageAsync(
            reference, "linux/arm64", host => new RegistryClient(host, new HttpClient(new StubHttpHandler(Serve))), CancellationToken.None);

    [Fact]
    public async Task A_tag_that_names_an_index_records_both_digests()
    {
        using OciImageBuilder builder = await PullAsync($"{Registry}/base:1.0");
        Assert.Equal(PlatformDigest, builder.BaseImageDigest);
        Assert.Equal(RegistryClient.ComputeDigest(Index), builder.BaseImageIndexDigest);
    }

    [Fact]
    public async Task A_single_platform_reference_has_no_index_digest()
    {
        using OciImageBuilder builder = await PullAsync($"{Registry}/base@{PlatformDigest}");
        Assert.Equal(PlatformDigest, builder.BaseImageDigest);
        Assert.Null(builder.BaseImageIndexDigest);
    }
}
