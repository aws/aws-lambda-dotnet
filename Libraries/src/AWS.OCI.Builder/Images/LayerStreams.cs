using System.Security.Cryptography;

namespace AWS.OCI.Builder.Images;

/// <summary>
/// A write-only pass-through stream that feeds every byte written to an <see cref="IncrementalHash"/> and counts
/// them before forwarding to an inner stream. Used to compute a layer's digests (and its compressed size) in the
/// same single pass that writes the layer, so the layer never has to be held in memory.
/// </summary>
/// <remarks>The inner stream is never disposed by this wrapper; its owner remains responsible for it.</remarks>
internal sealed class HashingWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash;

    public HashingWriteStream(Stream inner, IncrementalHash hash)
    {
        _inner = inner;
        _hash = hash;
    }

    /// <summary>The total number of bytes written through this stream.</summary>
    public long BytesWritten { get; private set; }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _hash.AppendData(buffer);
        BytesWritten += buffer.Length;
        _inner.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _hash.AppendData(buffer.Span);
        BytesWritten += buffer.Length;
        return _inner.WriteAsync(buffer, cancellationToken);
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>
/// A write-only pass-through stream that normalizes the 10-byte gzip member header as it is written, so the
/// header carries no build time or OS byte.
/// </summary>
/// <remarks>
/// <para>
/// A gzip member begins with a 10-byte header whose bytes 4–7 hold a modification time (MTIME) and whose byte 9
/// holds the source OS. <see cref="System.IO.Compression.GZipStream"/> fills these with values that vary by run
/// and by runtime (net8.0 writes the current time into MTIME; net9.0+ zeroes it). This stream rewrites MTIME to
/// zero and the OS byte to 0xFF ("unknown") as those bytes pass through.
/// </para>
/// <para>
/// The rewrite is driven by the absolute stream position, so it is correct however the writer chunks the header
/// (one byte at a time, or one write spanning the header and beyond). The caller's buffer is never modified: the
/// header slice is copied before it is patched. The inner stream is never disposed by this wrapper.
/// </para>
/// </remarks>
internal sealed class GzipHeaderNormalizingStream : Stream
{
    /// <summary>Length of the fixed gzip member header (RFC 1952 §2.3).</summary>
    internal const int HeaderLength = 10;

    private readonly Stream _inner;
    private long _position;

    public GzipHeaderNormalizingStream(Stream inner) => _inner = inner;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        int headerBytes = HeaderBytesIn(buffer.Length);
        if (headerBytes > 0)
        {
            Span<byte> patched = stackalloc byte[HeaderLength];
            patched = patched[..headerBytes];
            buffer[..headerBytes].CopyTo(patched);
            Normalize(patched, _position);
            _inner.Write(patched);
            _position += headerBytes;
            buffer = buffer[headerBytes..];
        }

        if (!buffer.IsEmpty)
        {
            _inner.Write(buffer);
            _position += buffer.Length;
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int headerBytes = HeaderBytesIn(buffer.Length);
        if (headerBytes > 0)
        {
            byte[] patched = buffer[..headerBytes].ToArray();
            Normalize(patched, _position);
            await _inner.WriteAsync(patched, cancellationToken).ConfigureAwait(false);
            _position += headerBytes;
            buffer = buffer[headerBytes..];
        }

        if (!buffer.IsEmpty)
        {
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            _position += buffer.Length;
        }
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>How many of the next <paramref name="count"/> bytes still fall inside the gzip header.</summary>
    private int HeaderBytesIn(int count)
        => _position >= HeaderLength ? 0 : (int)Math.Min(count, HeaderLength - _position);

    /// <summary>
    /// Patches the header bytes in <paramref name="segment"/>, which begins at absolute header offset
    /// <paramref name="start"/>: offsets 4–7 (MTIME) become zero and offset 9 (OS) becomes 0xFF.
    /// </summary>
    private static void Normalize(Span<byte> segment, long start)
    {
        for (int i = 0; i < segment.Length; i++)
        {
            long headerOffset = start + i;
            if (headerOffset is >= 4 and <= 7)
            {
                segment[i] = 0;
            }
            else if (headerOffset == 9)
            {
                segment[i] = 0xFF;
            }
        }
    }
}
