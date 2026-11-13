using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>
/// Tests for the security hardening: secrets never appear in ToString, names and digests are validated before
/// they reach request URLs, and response bodies from a registry are read with an upper bound.
/// </summary>
public class SecurityHardeningTests
{
    private const string ValidDigest = "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    // ---- Secrets never appear in ToString -----------------------------------------------------------------

    [Fact]
    public void BasicRegistryCredentials_ToString_redacts_password()
    {
        var creds = new BasicRegistryCredentials("AWS", "s3cr3t-token");

        string text = creds.ToString();

        Assert.DoesNotContain("s3cr3t-token", text);
        Assert.Contains("AWS", text);
        Assert.Equal(text, $"{creds}"); // interpolation goes through the same override
    }

    [Theory]
    [InlineData("{\"token\":\"tok-SECRET\",\"expires_in\":300}")]
    [InlineData("{\"access_token\":\"tok-SECRET\",\"expires_in\":300}")]
    [InlineData("{\"token\":\"tok-SECRET\",\"access_token\":\"tok-SECRET\"}")]
    public void TokenResponse_ToString_redacts_tokens(string json)
    {
        TokenResponse token = JsonSerializer.Deserialize(json, Images.OciJsonContext.TokenResponseInfo)!;

        Assert.DoesNotContain("tok-SECRET", token.ToString());
    }

    [Fact]
    public void CachedAuth_ToString_redacts_the_authorization_header()
    {
        // CachedAuth is a private nested type of the handler, so it is reached by reflection.
        Type cachedAuth = typeof(AuthHandshakeHandler).GetNestedType("CachedAuth", BindingFlags.NonPublic)!;
        object instance = Activator.CreateInstance(
            cachedAuth, new AuthenticationHeaderValue("Bearer", "tok-SECRET"), DateTimeOffset.UnixEpoch)!;

        string text = instance.ToString()!;

        Assert.DoesNotContain("tok-SECRET", text);
        Assert.Contains("Bearer", text);
    }

    // ---- Names and digests are validated before they reach URLs ------------------------------------------

    [Theory]
    [InlineData(ValidDigest, true)]
    [InlineData("sha256:E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", false)] // uppercase
    [InlineData("sha256:e3b0", false)]                                                          // too short
    [InlineData("sha512:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", false)]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", false)]        // no prefix
    [InlineData("sha256:abc/../../../victim/manifests/latest", false)]                            // path traversal
    [InlineData(ValidDigest + "\n", false)]                                                     // trailing newline
    [InlineData(null, false)]
    public void IsValidDigest(string? digest, bool expected)
        => Assert.Equal(expected, OciNames.IsValidDigest(digest));

    [Theory]
    [InlineData("myapp", true)]
    [InlineData("dotnet/aspnet", true)]
    [InlineData("//attacker.example/app", false)]
    [InlineData("myapp/../other", false)]
    [InlineData("myapp\n", false)]
    public void IsValidRepository(string repository, bool expected)
        => Assert.Equal(expected, OciNames.IsValidRepository(repository));

    [Theory]
    [InlineData("latest", true)]
    [InlineData("8.0-bookworm-slim", true)]
    [InlineData("..", false)]
    [InlineData("a?b", false)]
    [InlineData("a#b", false)]
    [InlineData("latest\n", false)]
    public void IsValidTag(string tag, bool expected)
        => Assert.Equal(expected, OciNames.IsValidTag(tag));

    [Theory]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp@sha256:abc/../../victim")]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp@sha256:ABC")]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp:..")]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp:a?b")]
    public void ImageReference_Parse_rejects_invalid_digest_or_tag(string reference)
        => Assert.Throws<ArgumentException>(() => ImageReference.Parse(reference));

    [Fact]
    public void ImageReference_Parse_accepts_valid_digest()
        => Assert.Equal(ValidDigest, ImageReference.Parse($"mcr.microsoft.com/dotnet/aspnet@{ValidDigest}").Reference);

    [Fact]
    public async Task RegistryClient_rejects_traversal_digest_without_sending_a_request()
    {
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new RegistryClient("123456789012.dkr.ecr.us-west-2.amazonaws.com", new HttpClient(handler));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.BlobExistsAsync("myapp", "sha256:abc/../../../victim/manifests/latest"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetBlobAsync("myapp", "sha256:abc/../../../victim/manifests/latest"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.TryMountBlobAsync("myapp", "sha256:bad", "base"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetManifestAsync("myapp", "../../victim"));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RegistryClient_rejects_repository_that_would_replace_the_host()
    {
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new RegistryClient("123456789012.dkr.ecr.us-west-2.amazonaws.com", new HttpClient(handler));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetBlobAsync("//attacker.example/app", ValidDigest));
        await Assert.ThrowsAsync<ArgumentException>(() => client.TryMountBlobAsync("myapp", ValidDigest, "//attacker.example/x"));

        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("sha256:abc/../../victim", 10)]
    [InlineData("sha256:ABC", 10)]
    [InlineData(ValidDigest, -1)]
    public void ValidateBaseDescriptor_rejects_bad_digest_or_negative_size(string digest, long size)
        => Assert.Throws<RegistryException>(
            () => OciImageBuilder.ValidateBaseDescriptor("example.com/base:1", "layer 0", digest, size));

    [Fact]
    public void ValidateBaseDescriptor_accepts_valid_descriptor()
        => OciImageBuilder.ValidateBaseDescriptor("example.com/base:1", "layer 0", ValidDigest, 0);

    // ---- Response bodies are read with an upper bound ---------------------------------------------------

    /// <summary>
    /// The most an endless body may be read past a limit: one pooled read buffer (ArrayPool rounds the
    /// requested 80 KiB up to 128 KiB).
    /// </summary>
    private const long ReadSlack = 128 * 1024;

    [Fact]
    public async Task GetManifestAsync_rejects_oversized_Content_Length_without_reading_the_body()
    {
        var body = new EndlessStream();
        var handler = new CountingHandler(_ =>
        {
            var content = new StreamContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypes.OciManifestV1);
            content.Headers.ContentLength = BoundedRead.MaxMetadataBytes + 1L;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var client = new RegistryClient("example.com", new HttpClient(handler));

        RegistryException ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetManifestAsync("repo", "latest"));

        Assert.Contains("limit", ex.Message);
        Assert.Equal(0, body.BytesServed);
    }

    [Fact]
    public async Task GetManifestAsync_stops_reading_an_endless_body_at_the_limit()
    {
        var body = new EndlessStream();
        var handler = new CountingHandler(_ =>
        {
            var content = new StreamContent(body); // no Content-Length: the size is only known by reading
            content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypes.OciManifestV1);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var client = new RegistryClient("example.com", new HttpClient(handler));

        await Assert.ThrowsAsync<RegistryException>(() => client.GetManifestAsync("repo", "latest"));

        Assert.InRange(body.BytesServed, BoundedRead.MaxMetadataBytes, BoundedRead.MaxMetadataBytes + ReadSlack);
    }

    [Fact]
    public async Task Error_body_is_read_only_up_to_its_limit()
    {
        var body = new EndlessStream();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StreamContent(body),
        });
        var client = new RegistryClient("example.com", new HttpClient(handler));

        await Assert.ThrowsAsync<RegistryRequestException>(() => client.GetManifestAsync("repo", "latest"));

        Assert.InRange(body.BytesServed, 1, BoundedRead.MaxErrorBodyBytes + ReadSlack);
    }

    [Fact]
    public async Task GetVerifiedBlobBytesAsync_stops_when_blob_exceeds_declared_size()
    {
        var body = new EndlessStream();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        var client = new RegistryClient("example.com", new HttpClient(handler));

        BlobSizeMismatchException ex = await Assert.ThrowsAsync<BlobSizeMismatchException>(
            () => client.GetVerifiedBlobBytesAsync("repo", ValidDigest, "base layer", expectedSize: 10));

        Assert.Equal(10, ex.DeclaredSize);
        Assert.Contains("larger", ex.Message);
        Assert.InRange(body.BytesServed, 11, 10 + ReadSlack);
    }

    [Fact]
    public async Task GetVerifiedBlobToFileAsync_stops_when_blob_exceeds_declared_size()
    {
        var body = new EndlessStream();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        var client = new RegistryClient("example.com", new HttpClient(handler));

        BlobSizeMismatchException ex = await Assert.ThrowsAsync<BlobSizeMismatchException>(
            () => client.GetVerifiedBlobToFileAsync("repo", ValidDigest, "base layer", expectedSize: 10));

        Assert.Equal(10, ex.DeclaredSize);
        Assert.InRange(body.BytesServed, 11, 10 + ReadSlack);
    }

    [Fact]
    public async Task Verified_downloads_reject_a_negative_expected_size()
    {
        var client = new RegistryClient("example.com", new HttpClient(new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.GetVerifiedBlobBytesAsync("repo", ValidDigest, "base layer", expectedSize: -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.GetVerifiedBlobToFileAsync("repo", ValidDigest, "base layer", expectedSize: -1));
    }

    [Fact]
    public async Task Oversized_token_response_is_rejected()
    {
        var tokenBody = new EndlessStream();
        var stub = new CountingHandler(request =>
        {
            if (request.RequestUri!.Host == "auth.example.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(tokenBody) };
            }
            var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            challenge.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                "Bearer", "realm=\"https://auth.example.com/token\",service=\"registry.example.com\""));
            return challenge;
        });
        var handler = new AuthHandshakeHandler(new AnonymousRegistryCredentialSource(), stub);
        using var http = new HttpClient(handler);

        InvalidAuthResponseException ex = await Assert.ThrowsAsync<InvalidAuthResponseException>(
            () => http.GetAsync("https://registry.example.com/v2/"));

        Assert.Contains("limit", ex.Message);
        Assert.InRange(tokenBody.BytesServed, BoundedRead.MaxMetadataBytes, BoundedRead.MaxMetadataBytes + ReadSlack);
    }

    [Fact]
    public async Task Unauthorized_body_is_drained_only_up_to_its_limit()
    {
        var challengeBody = new EndlessStream();
        var stub = new CountingHandler(request =>
        {
            if (request.Headers.Authorization is null)
            {
                var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StreamContent(challengeBody) };
                challenge.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Basic", "realm=\"registry\""));
                return challenge;
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var handler = new AuthHandshakeHandler(new StaticRegistryCredentialSource("registry.example.com", "AWS", "secret"), stub);
        using var http = new HttpClient(handler);

        using HttpResponseMessage response = await http.GetAsync("https://registry.example.com/v2/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.InRange(challengeBody.BytesServed, 1, BoundedRead.MaxErrorBodyBytes + ReadSlack);
    }

    // ---- Helpers ------------------------------------------------------------------------------------------

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int RequestCount { get; private set; }

        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responder(request));
        }
    }

    /// <summary>A non-seekable stream that never ends, counting how many bytes were read from it.</summary>
    private sealed class EndlessStream : Stream
    {
        public long BytesServed { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            buffer.Fill((byte)'a');
            BytesServed += buffer.Length;
            return buffer.Length;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Read(buffer.Span));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer.AsSpan(offset, count)));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
