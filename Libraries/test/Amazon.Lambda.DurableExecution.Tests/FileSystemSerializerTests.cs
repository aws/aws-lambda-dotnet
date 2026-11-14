// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Unit tests for <see cref="FileSystemSerializer"/>: storage modes (Always/Overflow),
/// path encodings (Uri/Hash), envelope shape, missing-file behavior, per-entity file
/// separation, composition with a compressing inner serializer, and the plain-path guard.
/// </summary>
public class FileSystemSerializerTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "fsser-" + Guid.NewGuid().ToString("N"));
    private readonly ILambdaSerializer _json = new DefaultLambdaJsonSerializer();

    public void Dispose()
    {
        try { if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true); } catch { /* best effort */ }
    }

    public sealed class Poco
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public override bool Equals(object? o) => o is Poco p && p.Id == Id && p.Name == Name;
        public override int GetHashCode() => Id;
    }

    private static DurableSerializationContext DurableArnCtx(string entity = "op-1") =>
        new(entity, "arn:aws:lambda:us-east-1:123456789012:function:fn:1/durable-execution/exec-1/inv-1");

    private static string Serialize<T>(FileSystemSerializer s, T value, DurableSerializationContext ctx)
    {
        using var ms = new MemoryStream();
        ((IDurableResultSerializer)s).Serialize(value, ms, ctx);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static T Deserialize<T>(FileSystemSerializer s, string envelope, DurableSerializationContext ctx)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(envelope));
        return ((IDurableResultSerializer)s).Deserialize<T>(ms, ctx);
    }

    [Fact]
    public void Always_WritesFile_EnvelopeIsPointer_AndRoundTrips()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Always);
        var value = new Poco { Id = 1, Name = "alice" };

        var envelope = Serialize(s, value, DurableArnCtx());

        Assert.Contains("\"file\"", envelope);
        Assert.DoesNotContain("\"data\"", envelope);
        Assert.NotEmpty(Directory.GetFiles(_base, "*.bin", SearchOption.AllDirectories));
        Assert.Equal(value, Deserialize<Poco>(s, envelope, DurableArnCtx()));
    }

    [Fact]
    public void Overflow_SmallValue_StoredInline_NoFile()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Overflow);
        var value = new Poco { Id = 7, Name = "small" };

        var envelope = Serialize(s, value, DurableArnCtx());

        Assert.Contains("\"data\"", envelope);
        Assert.DoesNotContain("\"file\"", envelope);
        Assert.False(Directory.Exists(_base) && Directory.GetFiles(_base, "*.bin", SearchOption.AllDirectories).Length > 0);
        Assert.Equal(value, Deserialize<Poco>(s, envelope, DurableArnCtx()));
    }

    [Fact]
    public void Overflow_LargeValue_OverflowsToFile_AndRoundTrips()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Overflow);
        var big = new string('x', 300 * 1024); // > ~256KB threshold once serialized

        var envelope = Serialize(s, big, DurableArnCtx());

        Assert.Contains("\"file\"", envelope);
        Assert.Equal(big, Deserialize<string>(s, envelope, DurableArnCtx()));
    }

    [Fact]
    public void Deserialize_MissingFile_ThrowsFileNotFound()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Always);
        var envelope = Serialize(s, new Poco { Id = 2, Name = "gone" }, DurableArnCtx());

        foreach (var f in Directory.GetFiles(_base, "*.bin", SearchOption.AllDirectories))
            File.Delete(f);

        Assert.Throws<FileNotFoundException>(() => Deserialize<Poco>(s, envelope, DurableArnCtx()));
    }

    [Fact]
    public void Deserialize_FilePointerOutsideBase_Throws()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Always);
        // A tampered/corrupted envelope pointing outside the base path must be rejected,
        // not read (guards against arbitrary file reads on the mounted filesystem).
        var evilPath = Path.Combine(Path.GetTempPath(), "evil-" + Guid.NewGuid().ToString("N") + ".bin");
        var envelope = "{\"file\":" + JsonSerializer.Serialize(evilPath) + "}";
        Assert.Throws<InvalidOperationException>(() => Deserialize<Poco>(s, envelope, DurableArnCtx()));
    }

    [Fact]
    public void UriPathEncoding_BuildsPerExecutionDirsFromArn()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Always, FileSystemPathEncoding.Uri);
        Serialize(s, new Poco { Id = 3, Name = "n" }, DurableArnCtx("entity-3"));

        var expectedDir = Path.Combine(_base, "fn", "exec-1", "inv-1");
        Assert.True(Directory.Exists(expectedDir), $"expected per-execution dir {expectedDir}");
        Assert.NotEmpty(Directory.GetFiles(expectedDir, "*.bin"));
    }

    [Fact]
    public void HashPathEncoding_UsesSha256HexSegments()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Always, FileSystemPathEncoding.Hash);
        Serialize(s, new Poco { Id = 4, Name = "n" }, DurableArnCtx("entity-4"));

        var file = Assert.Single(Directory.GetFiles(_base, "*.bin", SearchOption.AllDirectories));
        // Directory segment (ARN hash) and file stem (entity hash) are 64-char lowercase hex.
        var dirName = new DirectoryInfo(Path.GetDirectoryName(file)!).Name;
        var stem = Path.GetFileNameWithoutExtension(file);
        Assert.Matches("^[0-9a-f]{64}$", dirName);
        Assert.Matches("^[0-9a-f]{64}$", stem);
    }

    [Fact]
    public void DistinctEntityIds_WriteDistinctFiles()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Always);
        Serialize(s, new Poco { Id = 1, Name = "a" }, DurableArnCtx("op#0"));
        Serialize(s, new Poco { Id = 2, Name = "b" }, DurableArnCtx("op#1"));

        var files = Directory.GetFiles(_base, "*.bin", SearchOption.AllDirectories);
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void GzipInnerSerializer_Composes_CompressesFile_AndRoundTrips()
    {
        var gzip = new GzipJsonSerializer(_json);
        var compressing = new FileSystemSerializer(gzip, _base, FileSystemStorageMode.Always);
        var plain = new FileSystemSerializer(_json, _base + "-plain", FileSystemStorageMode.Always);

        var value = new string('a', 50 * 1024); // highly compressible

        var envelope = Serialize(compressing, value, DurableArnCtx("gz"));
        Serialize(plain, value, DurableArnCtx("gz"));

        var compressedFile = Assert.Single(Directory.GetFiles(_base, "*.bin", SearchOption.AllDirectories));
        var plainFile = Assert.Single(Directory.GetFiles(_base + "-plain", "*.bin", SearchOption.AllDirectories));

        Assert.True(new FileInfo(compressedFile).Length < new FileInfo(plainFile).Length,
            "gzip inner serializer should produce a smaller file than the plain JSON inner");
        Assert.Equal(value, Deserialize<string>(compressing, envelope, DurableArnCtx("gz")));

        try { Directory.Delete(_base + "-plain", true); } catch { /* best effort */ }
    }

    [Fact]
    public void PlainLambdaSerializerPath_Throws()
    {
        var s = (ILambdaSerializer)new FileSystemSerializer(_json, _base);
        using var ms = new MemoryStream();
        Assert.Throws<NotSupportedException>(() => s.Serialize(new Poco { Id = 1 }, ms));
        Assert.Throws<NotSupportedException>(() => s.Deserialize<Poco>(new MemoryStream(Encoding.UTF8.GetBytes("{}"))));
    }

    // ---- V7a / V4: crafted ARN must not escape the base path on the WRITE side ----

    [Fact]
    public void Serialize_CraftedArnWithTraversal_ThrowsOnWrite_DoesNotEscapeBase()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Always, FileSystemPathEncoding.Uri);

        // Matches the durable-execution ARN shape, but the captured function/execution
        // segments are ".." — resolving the per-execution write directory would traverse
        // above the configured base path. Uri encoding does NOT neutralize ".", so the
        // write path must reject this (the read path already had ValidatePathWithinBase).
        var evilArn = "arn:aws:lambda:us-east-1:123456789012:function:..:1/durable-execution/../inv-1";
        var ctx = new DurableSerializationContext("op-1", evilArn);

        var ex = Assert.Throws<InvalidOperationException>(() => Serialize(s, new Poco { Id = 1 }, ctx));
        Assert.Contains("base path", ex.Message);

        // Nothing was written outside (or inside) the base.
        Assert.False(Directory.Exists(_base) && Directory.GetFiles(_base, "*.bin", SearchOption.AllDirectories).Length > 0);
    }

    // ---- Symlinked base: a base path whose leaf is a symlink must not falsely reject reads/writes ----

    [Fact]
    public void SymlinkedBasePath_RoundTripsAndStaysContained()
    {
        // Model a real-world durable mount exposed through a symlink (e.g. an EFS mount
        // surfaced as /mnt/link -> /mnt/real). The base path handed to the serializer is
        // the *symlink*; every candidate path is built underneath it. The containment
        // guard resolves the symlinked base to its real target, so it must resolve the
        // candidates through the same symlink or it would reject every read and write.
        var realDir = Path.Combine(Path.GetTempPath(), "fsser-real-" + Guid.NewGuid().ToString("N"));
        var linkDir = Path.Combine(Path.GetTempPath(), "fsser-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(realDir);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkDir, realDir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                // Creating symlinks can require elevation (Windows without Developer Mode).
                // The behavior under test is filesystem-level, so skip where we cannot set up.
                return;
            }

            var s = new FileSystemSerializer(_json, linkDir, FileSystemStorageMode.Always);
            var value = new Poco { Id = 42, Name = "efs" };

            // Write through the symlinked base — must not throw "outside the configured base path".
            var envelope = Serialize(s, value, DurableArnCtx());
            Assert.Contains("\"file\"", envelope);

            // Read back through the same base — must not be rejected by the containment guard.
            var round = Deserialize<Poco>(s, envelope, DurableArnCtx());
            Assert.Equal(value, round);

            // The payload physically landed under the real target directory.
            Assert.True(
                Directory.GetFiles(realDir, "*.bin", SearchOption.AllDirectories).Length > 0,
                "offloaded payload should be written under the symlink's real target");
        }
        finally
        {
            // Delete the link itself (non-recursive) so cleanup never follows into the target.
            try { Directory.Delete(linkDir, recursive: false); } catch { /* best effort */ }
            try { Directory.Delete(realDir, recursive: true); } catch { /* best effort */ }
        }
    }

    // ---- V7b: ARN that doesn't match the pattern falls back to a single encoded segment ----

    [Fact]
    public void UriPathEncoding_NonMatchingArn_FallsBackToSingleEncodedDirectorySegment()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Always, FileSystemPathEncoding.Uri);

        // Not a durable-execution ARN; it even contains slashes. The whole value must be
        // URL-encoded into ONE directory segment directly under the base (slashes escaped
        // to %2F), never split into nested directories.
        var arn = "not-a-durable-execution-arn/with/slashes";
        var ctx = new DurableSerializationContext("entity-b", arn);

        var envelope = Serialize(s, new Poco { Id = 5, Name = "n" }, ctx);

        var expectedDir = Path.Combine(_base, Uri.EscapeDataString(arn));
        Assert.True(Directory.Exists(expectedDir), $"expected single fallback dir {expectedDir}");
        Assert.NotEmpty(Directory.GetFiles(expectedDir, "*.bin"));
        // The base has exactly one immediate child directory (the encoded segment).
        Assert.Single(Directory.GetDirectories(_base));
        Assert.Equal(new Poco { Id = 5, Name = "n" }, Deserialize<Poco>(s, envelope, ctx));
    }

    // ---- V7d: overflow inline/file boundary is inclusive at OverflowThresholdBytes ----

    /// <summary>Inner serializer that emits a fixed number of raw bytes, so a test can size the
    /// overflow envelope to the byte and pin the inline/file boundary.</summary>
    private sealed class FixedSizeSerializer : ILambdaSerializer
    {
        private readonly int _rawBytes;
        public FixedSizeSerializer(int rawBytes) => _rawBytes = rawBytes;
        public void Serialize<T>(T response, Stream responseStream) => responseStream.Write(new byte[_rawBytes], 0, _rawBytes);
        public T Deserialize<T>(Stream requestStream) => default!;
    }

    [Fact]
    public void Overflow_Boundary_LargestInlineStaysInline_NextSizeOverflows()
    {
        // OverflowThresholdBytes = 256*1024 - 1024 (private). Decision: envelopeBytes
        // <= threshold => inline. Base64 quantizes the payload to multiples of 4 bytes,
        // so the exact threshold byte isn't individually addressable; instead we pin the
        // transition: the largest raw payload whose envelope is <= threshold stays inline,
        // and one raw byte more (which pushes the envelope past threshold) overflows.
        const int threshold = (256 * 1024) - 1024;

        var inlineRaw = LargestRawInline(threshold);

        var inlineEnvelope = SerializeWithFixedInner(inlineRaw);
        Assert.Contains("\"data\"", inlineEnvelope);
        Assert.DoesNotContain("\"file\"", inlineEnvelope);

        var overflowEnvelope = SerializeWithFixedInner(inlineRaw + 1);
        Assert.Contains("\"file\"", overflowEnvelope);
        Assert.DoesNotContain("\"data\"", overflowEnvelope);

        string SerializeWithFixedInner(int rawBytes)
        {
            var s = new FileSystemSerializer(new FixedSizeSerializer(rawBytes), _base, FileSystemStorageMode.Overflow);
            return Serialize(s, 0, DurableArnCtx("boundary-" + rawBytes));
        }
    }

    // Largest raw byte count whose inline envelope ({"data":"<base64>"}) is <= threshold.
    private static int LargestRawInline(int threshold)
    {
        // Envelope overhead around the base64 body: {"data":"..."} = 9 + 2 = 11 bytes.
        for (var n = threshold; n > 0; n--)
        {
            var base64Len = ((n + 2) / 3) * 4;
            if (11 + base64Len <= threshold)
                return n;
        }
        return 0;
    }

    // ---- V6: corrupted inline base64 surfaces a descriptive InvalidOperationException ----

    [Fact]
    public void Deserialize_CorruptedInlineBase64_ThrowsDescriptiveInvalidOperationException()
    {
        var s = new FileSystemSerializer(_json, _base, FileSystemStorageMode.Overflow);
        // A bare FormatException from Convert.FromBase64String would be inconsistent with
        // this method's sibling descriptive InvalidOperationException checks.
        var envelope = "{\"data\":\"not valid base64 !!!\"}";

        var ex = Assert.Throws<InvalidOperationException>(() => Deserialize<Poco>(s, envelope, DurableArnCtx()));
        Assert.Contains("base64", ex.Message);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    // ---- V5: a relative basePath is normalized to an absolute pointer in the ctor ----

    [Fact]
    public void Ctor_RelativeBasePath_ProducesAbsoluteFilePointer()
    {
        var relative = "fsser-rel-" + Guid.NewGuid().ToString("N");
        var s = new FileSystemSerializer(_json, relative, FileSystemStorageMode.Always);
        try
        {
            var envelope = Serialize(s, new Poco { Id = 1, Name = "n" }, DurableArnCtx());

            using var doc = JsonDocument.Parse(envelope);
            var file = doc.RootElement.GetProperty("file").GetString()!;
            // Normalizing _basePath in the ctor makes the stored pointer absolute, so a later
            // invocation with a different CWD resolves it to the same file.
            Assert.True(Path.IsPathFullyQualified(file), $"pointer should be absolute but was '{file}'");
        }
        finally
        {
            try { Directory.Delete(Path.GetFullPath(relative), recursive: true); } catch { /* best effort */ }
        }
    }

    // ---- Comment 4: read handle must not block a concurrent atomic replace ----

    /// <summary>Inner serializer whose <c>Deserialize</c> signals that the read stream is
    /// open, then blocks until released — lets a test hold the real read FileStream open
    /// and attempt a concurrent atomic replace (File.Move overwrite) over it.</summary>
    private sealed class BlockingReadSerializer : ILambdaSerializer
    {
        private readonly ILambdaSerializer _inner = new DefaultLambdaJsonSerializer();
        public ManualResetEventSlim Reading { get; } = new(false);
        public ManualResetEventSlim Proceed { get; } = new(false);

        public void Serialize<T>(T response, Stream responseStream) => _inner.Serialize(response, responseStream);

        public T Deserialize<T>(Stream requestStream)
        {
            Reading.Set();
            if (!Proceed.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Proceed signal was not received.");
            return _inner.Deserialize<T>(requestStream);
        }
    }

    [Fact]
    public async Task Deserialize_ReadHandleOpen_DoesNotBlockConcurrentAtomicReplace()
    {
        // NOTE: this test only meaningfully exercises the FileShare.Delete regression on
        // Windows/macOS. On POSIX/Linux (CI), rename-over-an-open-fd (File.Move overwrite)
        // ALWAYS succeeds regardless of the reader's share flags — the reader keeps the old
        // inode — so on Linux the Assert.Null(moveEx) below would pass even WITHOUT
        // FileShare.Delete, making it tautological there. It is the Windows/macOS run
        // (dev/test) that actually guards the fix, where a read handle opened without
        // FileShare.Delete blocks the rename-over and throws IOException.
        //
        // The read path opens the offloaded file with FileShare.Read | FileShare.Delete so
        // a concurrent re-serialize (File.Move overwrite, the write path's atomic replace)
        // can proceed while a replay-read is in flight. On Linux the open fd references the
        // original inode regardless; on Windows/macOS a read handle opened WITHOUT
        // FileShare.Delete blocks the rename-over and throws IOException. This test holds
        // the real read FileStream open and asserts the replace succeeds.
        var blocking = new BlockingReadSerializer();
        var s = new FileSystemSerializer(blocking, _base, FileSystemStorageMode.Always);

        var envelope = Serialize(s, new Poco { Id = 1, Name = "orig" }, DurableArnCtx("op#0"));
        using var doc = JsonDocument.Parse(envelope);
        var filePath = doc.RootElement.GetProperty("file").GetString()!;

        // Open our real read FileStream, then block inside the inner deserialize while the
        // handle is still open.
        var readTask = Task.Run(() => Deserialize<Poco>(s, envelope, DurableArnCtx("op#0")));
        Assert.True(blocking.Reading.Wait(TimeSpan.FromSeconds(10)), "the read never opened the file handle");

        // Replace the file the way the write path does: a sibling temp file moved over the
        // target while the reader holds it open.
        var tmp = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(tmp, Encoding.UTF8.GetBytes("{\"Id\":2,\"Name\":\"replaced\"}"));
        var moveEx = Record.Exception(() => File.Move(tmp, filePath, overwrite: true));
        Assert.Null(moveEx); // FileShare.Delete keeps the in-flight reader from blocking the replace

        // Release the reader; via its already-open handle it still reads the ORIGINAL bytes.
        blocking.Proceed.Set();
        var read = await readTask;
        Assert.Equal(new Poco { Id = 1, Name = "orig" }, read);
    }

    [Fact]
    public void InnerLessConstructor_BoundToDefaultInner_RoundTrips()
    {
        // The inner-less constructor defers to the globally-registered serializer, which the
        // durable runtime binds via IDefaultInnerSerializer.WithDefaultInner before use.
        var innerLess = new FileSystemSerializer(_base, FileSystemStorageMode.Always);
        var bound = (FileSystemSerializer)((IDefaultInnerSerializer)innerLess).WithDefaultInner(_json);
        var value = new Poco { Id = 9, Name = "bound" };

        var envelope = Serialize(bound, value, DurableArnCtx());

        Assert.Contains("\"file\"", envelope);
        Assert.Equal(value, Deserialize<Poco>(bound, envelope, DurableArnCtx()));
    }

    [Fact]
    public void WithDefaultInner_ExplicitInnerWins_ReturnsSameInstance()
    {
        // A caller-supplied inner is never overridden by the runtime's default.
        var explicitInner = new FileSystemSerializer(_json, _base);
        var result = ((IDefaultInnerSerializer)explicitInner).WithDefaultInner(new DefaultLambdaJsonSerializer());
        Assert.Same(explicitInner, result);
    }

    [Fact]
    public void InnerLessConstructor_WithoutBinding_Throws()
    {
        // Used without the runtime binding a default inner (e.g. outside a durable operation
        // slot), there is no serializer to convert the value — fail with a clear message.
        var innerLess = new FileSystemSerializer(_base, FileSystemStorageMode.Always);
        Assert.Throws<InvalidOperationException>(
            () => Serialize(innerLess, new Poco { Id = 1, Name = "x" }, DurableArnCtx()));
    }

    private sealed class GzipJsonSerializer : ILambdaSerializer
    {
        private readonly ILambdaSerializer _json;
        public GzipJsonSerializer(ILambdaSerializer json) => _json = json;

        public void Serialize<T>(T response, Stream responseStream)
        {
            using var gz = new GZipStream(responseStream, CompressionLevel.Optimal, leaveOpen: true);
            _json.Serialize(response, gz);
        }

        public T Deserialize<T>(Stream requestStream)
        {
            using var gz = new GZipStream(requestStream, CompressionMode.Decompress, leaveOpen: true);
            return _json.Deserialize<T>(gz);
        }
    }
}
