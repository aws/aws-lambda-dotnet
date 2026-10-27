namespace Amazon.OCI.Builder;

/// <summary>
/// Options controlling how an image is pushed to Amazon ECR.
/// </summary>
/// <remarks>
/// All properties are optional. The ECR authorization token and target region are not configured here: the
/// token is passed directly to <see cref="OciImageBuilder.PushToEcrAsync"/>, and the region is taken from the
/// ECR host in the target reference.
/// </remarks>
public sealed class EcrPushOptions
{
    /// <summary>
    /// The default value of <see cref="MaxInMemoryLayerBytes"/>: 100&#160;MiB (binary megabytes). Chosen so that
    /// common base images — whose individual layers run to tens of MiB — are copied entirely in memory, while
    /// genuinely large layers spill to disk.
    /// </summary>
    public const long DefaultMaxInMemoryLayerBytes = 100L * 1024 * 1024;

    /// <summary>
    /// The size threshold, in bytes, that decides how a base-image layer is copied to ECR during a push. A base
    /// layer whose size is at or below this value is buffered in memory; one that exceeds it is streamed through a
    /// temporary file on disk instead, bounding peak memory for very large base images. Defaults to
    /// <see cref="DefaultMaxInMemoryLayerBytes"/> (100&#160;MiB, binary).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This affects only the copy of <em>base-image</em> layers (which are downloaded from the source registry and
    /// re-uploaded to ECR). Layers built by this library are always written to a temporary file when they are added
    /// (see <see cref="OciImageBuilder.AddLayerAsync"/>) and uploaded from that file during the push, regardless
    /// of this setting.
    /// </para>
    /// <para>
    /// Data integrity is preserved on both paths: the in-memory path verifies the downloaded bytes against the
    /// layer's digest before upload, and the spill-to-disk path verifies the digest while streaming to disk
    /// <em>and</em> re-reads the file from disk to verify it again before upload — so a faulty disk write that
    /// corrupts the spilled layer is caught and the push fails rather than committing corrupt bytes to ECR. The
    /// temporary file is deleted after the upload completes (and on failure or cancellation).
    /// </para>
    /// <para>
    /// The value is interpreted as binary bytes. Set it very high to always buffer in memory, or to <c>0</c> to
    /// always spill to disk. A negative value is rejected at push time.
    /// </para>
    /// </remarks>
    public long MaxInMemoryLayerBytes { get; init; } = DefaultMaxInMemoryLayerBytes;

    /// <summary>
    /// An optional sink that receives a <see cref="LayerPushProgress"/> event as each layer is pushed, letting a
    /// caller observe push progress (for example to render a per-layer progress bar). Defaults to
    /// <see langword="null"/>, meaning no progress is reported.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each layer reports a <see cref="LayerPushPhase.Started"/> event, zero or more
    /// <see cref="LayerPushPhase.Uploading"/> events as its bytes are sent, and a terminal
    /// <see cref="LayerPushPhase.Completed"/> event; a layer the registry already has reports a single
    /// <see cref="LayerPushPhase.Completed"/> event with <see cref="LayerPushProgress.IsSkipped"/> set. Each event
    /// carries the layer's stable <see cref="LayerPushProgress.LayerIndex"/> / <see cref="LayerPushProgress.Digest"/>,
    /// so the contract holds even if layers are later uploaded concurrently — see <see cref="LayerPushProgress"/>.
    /// </para>
    /// <para>
    /// Use <see cref="System.Progress{T}"/> for the common case: it captures the current
    /// <see cref="System.Threading.SynchronizationContext"/> and raises callbacks on it, which keeps UI updates on
    /// the UI thread. Any custom implementation must be thread-safe, since a future parallel push may report from
    /// multiple threads at once.
    /// </para>
    /// </remarks>
    public IProgress<LayerPushProgress>? Progress { get; init; }

    /// <summary>
    /// The HTTP timeout applied to the registry client used for the push. Because a whole layer blob is sent in a
    /// single <c>PATCH</c>, the default 100-second <see cref="System.Net.Http.HttpClient"/> timeout would cancel a
    /// large-layer upload on a slow connection even while the caller's cancellation token is still valid. Defaults
    /// to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> (no client-imposed timeout), so an upload is
    /// bounded only by the <see cref="System.Threading.CancellationToken"/> passed to
    /// <see cref="OciImageBuilder.PushToEcrAsync"/> — pass a token with your own deadline to bound it. Set a finite
    /// value to impose a per-request timeout instead.
    /// </summary>
    /// <remarks>
    /// A finite value must be positive, or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>; any other value
    /// (zero or negative) is rejected at push time.
    /// </remarks>
    public TimeSpan UploadTimeout { get; init; } = System.Threading.Timeout.InfiniteTimeSpan;
}
