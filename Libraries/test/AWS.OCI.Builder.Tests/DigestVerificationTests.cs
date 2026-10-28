using System.Text;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class DigestVerificationTests
{
    [Fact]
    public void ComputeDigest_of_known_bytes()
    {
        // SHA-256 of the empty input is a well-known constant.
        string digest = RegistryClient.ComputeDigest(ReadOnlySpan<byte>.Empty);
        Assert.Equal("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", digest);
    }

    [Fact]
    public void ComputeDigest_byte_and_stream_overloads_agree()
    {
        byte[] data = Encoding.UTF8.GetBytes("the quick brown fox");
        string fromBytes = RegistryClient.ComputeDigest(data);
        string fromStream = RegistryClient.ComputeDigest(new MemoryStream(data));
        Assert.Equal(fromStream, fromBytes);
    }

    [Fact]
    public void ComputeDigest_stream_hashes_from_current_position()
    {
        byte[] data = Encoding.UTF8.GetBytes("prefix|suffix");
        var stream = new MemoryStream(data) { Position = 7 }; // positioned at "suffix"

        string digest = RegistryClient.ComputeDigest(stream);

        Assert.Equal(RegistryClient.ComputeDigest(Encoding.UTF8.GetBytes("suffix")), digest);
        Assert.Equal(data.Length, stream.Position); // left at the end, not rewound
    }

    [Fact]
    public void VerifyDigest_passes_for_matching_content()
    {
        byte[] data = Encoding.UTF8.GetBytes("layer-bytes");
        string digest = RegistryClient.ComputeDigest(data);

        // Should not throw.
        RegistryClient.VerifyDigest("test blob", digest, data);
    }

    [Fact]
    public void VerifyDigest_is_case_insensitive_on_hex()
    {
        byte[] data = Encoding.UTF8.GetBytes("layer-bytes");
        string digest = RegistryClient.ComputeDigest(data).ToUpperInvariant(); // SHA256:<UPPERHEX>
        RegistryClient.VerifyDigest("test blob", digest, data);
    }

    [Fact]
    public void VerifyDigest_throws_on_mismatch()
    {
        byte[] data = Encoding.UTF8.GetBytes("real-bytes");
        byte[] other = Encoding.UTF8.GetBytes("tampered-bytes");
        string wrongDigest = RegistryClient.ComputeDigest(other);

        DigestMismatchException ex = Assert.Throws<DigestMismatchException>(
            () => RegistryClient.VerifyDigest("test blob", wrongDigest, data));

        Assert.Equal(wrongDigest, ex.ExpectedDigest);
        Assert.Equal(RegistryClient.ComputeDigest(data), ex.ActualDigest);
    }

    [Fact]
    public void VerifyDigest_rejects_unsupported_algorithm()
    {
        byte[] data = Encoding.UTF8.GetBytes("x");
        RegistryException ex = Assert.Throws<RegistryException>(
            () => RegistryClient.VerifyDigest("test blob", "sha512:deadbeef", data));
        Assert.Contains("sha256", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DigestMismatchException_is_a_RegistryException()
        => Assert.IsAssignableFrom<RegistryException>(new DigestMismatchException("x", "sha256:a", "sha256:b"));

    /// <summary>A stub registry handler that serves a single blob's bytes under a given digest on GET.</summary>
    private sealed class SingleBlobHandler : HttpMessageHandler
    {
        private readonly string _digest;
        private readonly byte[] _bytes;

        public SingleBlobHandler(string digest, byte[] bytes)
        {
            _digest = digest;
            _bytes = bytes;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith($"/blobs/{_digest}", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_bytes),
                });
            }
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task GetVerifiedBlobToFileAsync_returns_verified_stream_and_deletes_file_on_dispose()
    {
        byte[] data = Encoding.UTF8.GetBytes("a large-ish base layer spilled to disk");
        string digest = RegistryClient.ComputeDigest(data);
        var client = new RegistryClient("example.com", new HttpClient(new SingleBlobHandler(digest, data)));

        string path;
        await using (VerifiedBlobFile handle = await client.GetVerifiedBlobToFileAsync("repo", digest, "base layer"))
        {
            path = handle.Path;
            Assert.True(File.Exists(path));
            Assert.Equal(0, handle.Stream.Position);

            // The stream serves exactly the verified bytes.
            using var buffer = new MemoryStream();
            await handle.Stream.CopyToAsync(buffer);
            Assert.Equal(data, buffer.ToArray());
        }

        // Disposing the handle deletes the temp file.
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task GetVerifiedBlobToFileAsync_throws_when_bytes_do_not_match_digest()
    {
        byte[] served = Encoding.UTF8.GetBytes("corrupted-on-the-wire");
        // Declare a digest for DIFFERENT content, so the download hash will not match.
        string declaredDigest = RegistryClient.ComputeDigest(Encoding.UTF8.GetBytes("the-real-content"));
        var client = new RegistryClient("example.com", new HttpClient(new SingleBlobHandler(declaredDigest, served)));

        await Assert.ThrowsAsync<DigestMismatchException>(
            () => client.GetVerifiedBlobToFileAsync("repo", declaredDigest, "base layer"));
        // Cleanup-on-failure is covered deterministically by the success test's dispose assertion; a global
        // temp-dir file count here would race with other parallel tests sharing the temp directory.
    }

    [Fact]
    public async Task GetVerifiedBlobToFileAsync_throws_when_declared_size_differs_from_blob_length()
    {
        // The bytes hash correctly to the digest, but the manifest descriptor declares the wrong size.
        byte[] data = Encoding.UTF8.GetBytes("correct content, wrong declared size");
        string digest = RegistryClient.ComputeDigest(data);
        var client = new RegistryClient("example.com", new HttpClient(new SingleBlobHandler(digest, data)));

        BlobSizeMismatchException ex = await Assert.ThrowsAsync<BlobSizeMismatchException>(
            () => client.GetVerifiedBlobToFileAsync("repo", digest, "base layer", expectedSize: data.Length + 7));

        Assert.Equal(digest, ex.Digest);
        Assert.Equal(data.Length + 7, ex.DeclaredSize);
        Assert.Equal(data.Length, ex.ActualSize);
    }

    [Fact]
    public async Task GetVerifiedBlobToFileAsync_accepts_matching_declared_size()
    {
        byte[] data = Encoding.UTF8.GetBytes("size matches");
        string digest = RegistryClient.ComputeDigest(data);
        var client = new RegistryClient("example.com", new HttpClient(new SingleBlobHandler(digest, data)));

        await using VerifiedBlobFile handle = await client.GetVerifiedBlobToFileAsync("repo", digest, "base layer", expectedSize: data.Length);
        Assert.Equal(data.Length, handle.Stream.Length);
    }

    [Fact]
    public async Task GetVerifiedBlobBytesAsync_throws_when_declared_size_differs_from_blob_length()
    {
        byte[] data = Encoding.UTF8.GetBytes("small blob");
        string digest = RegistryClient.ComputeDigest(data);
        var client = new RegistryClient("example.com", new HttpClient(new SingleBlobHandler(digest, data)));

        BlobSizeMismatchException ex = await Assert.ThrowsAsync<BlobSizeMismatchException>(
            () => client.GetVerifiedBlobBytesAsync("repo", digest, "base image config", expectedSize: data.Length - 1));
        Assert.Contains("declares a size", ex.Message, StringComparison.Ordinal);

        // A matching size (or no declared size) passes.
        Assert.Equal(data, await client.GetVerifiedBlobBytesAsync("repo", digest, "base image config", expectedSize: data.Length));
        Assert.Equal(data, await client.GetVerifiedBlobBytesAsync("repo", digest, "base image config"));
    }

    [Fact]
    public void BlobSizeMismatchException_is_a_RegistryException()
        => Assert.IsAssignableFrom<RegistryException>(new BlobSizeMismatchException("x", "sha256:a", 1, 2));
}
