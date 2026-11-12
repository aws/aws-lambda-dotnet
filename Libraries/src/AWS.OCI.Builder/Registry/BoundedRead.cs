using System.Buffers;

namespace AWS.OCI.Builder.Registry;

/// <summary>
/// Reads HTTP bodies with an upper bound, so a malicious or broken registry cannot exhaust memory by sending an
/// arbitrarily large manifest, token response or error body.
/// </summary>
internal static class BoundedRead
{
    /// <summary>
    /// The limit for metadata documents: manifests, image indexes, image configs and token responses. Matches the
    /// 4 MiB manifest limit used by containerd and the distribution registry; real documents are far smaller.
    /// </summary>
    public const int MaxMetadataBytes = 4 * 1024 * 1024;

    /// <summary>How much of an error response body is read for the exception message.</summary>
    public const int MaxErrorBodyBytes = 4 * 1024;

    /// <summary>
    /// Reads at most <paramref name="limit"/> bytes of <paramref name="content"/>. <c>Exceeded</c> is
    /// <see langword="true"/> when the body is larger than the limit, either because its <c>Content-Length</c>
    /// says so (nothing is read) or because more bytes arrived than allowed (reading stops right there).
    /// </summary>
    public static async Task<(byte[] Data, bool Exceeded)> ReadAsync(HttpContent content, long limit, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long declared && declared > limit)
        {
            return (Array.Empty<byte>(), true);
        }

        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(stream, limit, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads at most <paramref name="limit"/> bytes of <paramref name="source"/>, stopping as soon as more than
    /// <paramref name="limit"/> bytes are seen. <c>Data</c> holds the bytes up to the limit.
    /// </summary>
    public static async Task<(byte[] Data, bool Exceeded)> ReadAsync(Stream source, long limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        using var buffer = new MemoryStream();
        byte[] chunk = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return (buffer.ToArray(), false);
                }

                long room = limit - buffer.Length;
                if (read > room)
                {
                    buffer.Write(chunk, 0, (int)room);
                    return (buffer.ToArray(), true);
                }
                buffer.Write(chunk, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }
}
