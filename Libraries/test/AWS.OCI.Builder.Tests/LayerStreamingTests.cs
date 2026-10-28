using System.IO.Compression;
using System.Security.Cryptography;
using Amazon.OCI.Builder;
using Amazon.OCI.Builder.Images;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>
/// Tests for the streaming layer pipeline: the gzip-header normalizer and hashing pass-through streams, and the
/// <see cref="Layer.CreateAsync"/> destination-stream contract.
/// </summary>
public class LayerStreamingTests : IDisposable
{
    private readonly string _tempRoot;

    public LayerStreamingTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "aws-oci-builder-stream-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best effort cleanup */ }
    }

    private FileManifest SampleManifest()
    {
        string file = Path.Combine(_tempRoot, "app.dll");
        // Incompressible content so deflate emits several blocks and many writes reach the sink.
        var data = new byte[512 * 1024];
        new Random(42).NextBytes(data);
        File.WriteAllBytes(file, data);
        return new FileManifest { { file, "/app/app.dll" } };
    }

    // ---- GzipHeaderNormalizingStream -------------------------------------------------------------

    /// <summary>A fake "gzip" byte sequence whose header has non-normalized MTIME/OS fields, plus a body.</summary>
    private static byte[] RawGzipLikeBytes()
    {
        byte[] bytes = Enumerable.Range(0, 32).Select(i => (byte)(0xA0 + i)).ToArray();
        bytes[0] = 0x1F; bytes[1] = 0x8B; // magic, untouched by the normalizer
        return bytes;
    }

    private static byte[] Expected(byte[] raw)
    {
        byte[] expected = (byte[])raw.Clone();
        expected[4] = expected[5] = expected[6] = expected[7] = 0;
        expected[9] = 0xFF;
        return expected;
    }

    public static IEnumerable<object[]> ChunkPatterns() => new[]
    {
        new object[] { new[] { 32 } },                // whole buffer in one write
        new object[] { Enumerable.Repeat(1, 32).ToArray() }, // one byte at a time
        new object[] { new[] { 3, 2, 4, 1, 22 } },    // splits inside MTIME and right before OS
        new object[] { new[] { 9, 1, 22 } },          // OS byte written alone
        new object[] { new[] { 10, 22 } },            // exactly the header, then the body
        new object[] { new[] { 11, 21 } },            // a write spanning the header boundary
    };

    [Theory]
    [MemberData(nameof(ChunkPatterns))]
    public void Normalizer_patches_header_regardless_of_write_chunking(int[] chunks)
    {
        byte[] raw = RawGzipLikeBytes();
        byte[] original = (byte[])raw.Clone();
        var sink = new MemoryStream();
        var normalizer = new GzipHeaderNormalizingStream(sink);

        int offset = 0;
        foreach (int chunk in chunks)
        {
            normalizer.Write(raw, offset, chunk);
            offset += chunk;
        }

        Assert.Equal(Expected(raw), sink.ToArray());
        Assert.Equal(original, raw); // the caller's buffer is never modified
    }

    [Theory]
    [MemberData(nameof(ChunkPatterns))]
    public async Task Normalizer_patches_header_regardless_of_write_chunking_async(int[] chunks)
    {
        byte[] raw = RawGzipLikeBytes();
        byte[] original = (byte[])raw.Clone();
        var sink = new MemoryStream();
        var normalizer = new GzipHeaderNormalizingStream(sink);

        int offset = 0;
        foreach (int chunk in chunks)
        {
            await normalizer.WriteAsync(raw.AsMemory(offset, chunk));
            offset += chunk;
        }

        Assert.Equal(Expected(raw), sink.ToArray());
        Assert.Equal(original, raw);
    }

    // ---- HashingWriteStream -----------------------------------------------------------------------

    [Fact]
    public async Task HashingWriteStream_hashes_and_counts_bytes_across_sync_and_async_writes()
    {
        byte[] data = Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray();
        var sink = new MemoryStream();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var stream = new HashingWriteStream(sink, hash);

        stream.Write(data, 0, 100);
        stream.Write(data.AsSpan(100, 400));
        await stream.WriteAsync(data.AsMemory(500, 500));

        Assert.Equal(1000, stream.BytesWritten);
        Assert.Equal(data, sink.ToArray());
        Assert.Equal(SHA256.HashData(data), hash.GetHashAndReset());
    }

    // ---- Layer.CreateAsync destination contract ---------------------------------------------------

    [Fact]
    public async Task CreateAsync_streams_bytes_matching_the_reported_digests_and_size()
    {
        var destination = new MemoryStream();
        Layer layer = await Layer.CreateAsync(SampleManifest(), destination);
        byte[] blob = destination.ToArray();

        Assert.Equal(blob.LongLength, layer.Size);
        Assert.Equal(RegistryDigest(blob), layer.Digest);
        Assert.Equal(RegistryDigest(Gunzip(blob)), layer.DiffId);
    }

    [Fact]
    public async Task CreateAsync_output_matches_one_shot_compression_of_the_same_tar()
    {
        // Guards that streaming the tar into the compressor in many small writes produces exactly the bytes the
        // previous implementation produced by compressing the whole buffered tar in a single write.
        var destination = new MemoryStream();
        await Layer.CreateAsync(SampleManifest(), destination);
        byte[] streamed = destination.ToArray();

        byte[] oneShot = OneShotCompress(Gunzip(streamed));

        Assert.Equal(oneShot, streamed);
    }

    [Fact]
    public async Task CreateAsync_normalizes_the_gzip_header()
    {
        var destination = new MemoryStream();
        await Layer.CreateAsync(SampleManifest(), destination);
        byte[] blob = destination.ToArray();

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, blob[4..8]); // MTIME
        Assert.Equal(0xFF, blob[9]);                           // OS = unknown
    }

    [Fact]
    public async Task CreateAsync_leaves_destination_open_and_appends_at_its_current_position()
    {
        byte[] prefix = "existing-content"u8.ToArray();
        var destination = new MemoryStream();
        destination.Write(prefix);

        Layer layer = await Layer.CreateAsync(SampleManifest(), destination);

        Assert.True(destination.CanWrite); // not disposed/closed
        byte[] all = destination.ToArray();
        Assert.Equal(prefix, all[..prefix.Length]);
        Assert.Equal(prefix.Length + layer.Size, all.LongLength);
        Assert.Equal(layer.Digest, RegistryDigest(all[prefix.Length..]));
    }

    [Fact]
    public async Task CreateAsync_validates_the_destination()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Layer.CreateAsync(SampleManifest(), null!));

        using var readOnly = new MemoryStream(new byte[16], writable: false);
        await Assert.ThrowsAsync<ArgumentException>(() => Layer.CreateAsync(SampleManifest(), readOnly));
    }

    // ---- helpers ----------------------------------------------------------------------------------

    private static string RegistryDigest(byte[] data)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static byte[] Gunzip(byte[] gz)
    {
        using var gunzip = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress);
        using var tar = new MemoryStream();
        gunzip.CopyTo(tar);
        return tar.ToArray();
    }

    /// <summary>The previous implementation: compress the whole tar in one write, then normalize the header.</summary>
    private static byte[] OneShotCompress(byte[] tar)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(tar, 0, tar.Length);
        }
        byte[] bytes = output.ToArray();
        bytes[4] = bytes[5] = bytes[6] = bytes[7] = 0;
        bytes[9] = 0xFF;
        return bytes;
    }
}
