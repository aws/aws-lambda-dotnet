namespace AWS.OCI.Builder;

/// <summary>
/// The lifecycle phase a layer is in when a <see cref="LayerPushProgress"/> event is reported.
/// </summary>
/// <remarks>
/// Every layer — whether uploaded or skipped — terminates on <see cref="Completed"/>, so a consumer counting
/// finished layers increments on a single phase without branching on whether the layer was skipped. A skipped
/// layer is reported as a single <see cref="Completed"/> event with <see cref="LayerPushProgress.IsSkipped"/> set
/// to <see langword="true"/>; it does not emit a preceding <see cref="Started"/> because there is no upload to
/// announce.
/// </remarks>
public enum LayerPushPhase
{
    /// <summary>The layer's upload is about to begin. <see cref="LayerPushProgress.BytesPushed"/> is 0.</summary>
    Started,

    /// <summary>
    /// Bytes of the layer have been sent. <see cref="LayerPushProgress.BytesPushed"/> rises toward
    /// <see cref="LayerPushProgress.LayerSizeBytes"/> across successive events.
    /// </summary>
    Uploading,

    /// <summary>
    /// The layer is finished. For an uploaded layer <see cref="LayerPushProgress.BytesPushed"/> equals
    /// <see cref="LayerPushProgress.LayerSizeBytes"/>; for a skipped layer (already present in the registry)
    /// <see cref="LayerPushProgress.IsSkipped"/> is <see langword="true"/> and
    /// <see cref="LayerPushProgress.BytesPushed"/> is 0.
    /// </summary>
    Completed,
}

/// <summary>
/// Distinguishes a layer copied from the base image from one built locally by this library during the push.
/// </summary>
public enum LayerKind
{
    /// <summary>A layer of the base image, copied from the source registry into the target repository.</summary>
    BaseLayer,

    /// <summary>A layer produced by this build (via <see cref="OciImageBuilder.AddLayerAsync"/>).</summary>
    BuiltLayer,
}

/// <summary>
/// A progress event reported while an image is being pushed to Amazon ECR, describing the state of one layer.
/// Subscribe by setting <see cref="EcrPushOptions.Progress"/> to an <see cref="IProgress{T}"/> of this type.
/// </summary>
/// <remarks>
/// <para>
/// Each layer has a stable identity — <see cref="LayerIndex"/>, <see cref="TotalLayers"/>, <see cref="Digest"/>
/// and <see cref="Kind"/> — that is assigned once, before any upload begins, and never changes across the events
/// for that layer. Consumers should therefore key their state (for example a per-layer progress bar) by
/// <see cref="LayerIndex"/> or <see cref="Digest"/>, <em>not</em> by the order events arrive.
/// </para>
/// <para>
/// This matters because the push may upload layers concurrently in a future revision of this library. When it
/// does, events for different layers will interleave and a layer may finish before an earlier-indexed one — but
/// because each event carries its layer's fixed index and digest, this contract is unchanged and consumers keyed
/// by index/digest continue to work without modification.
/// </para>
/// <para>
/// Only image <em>layers</em> are reported. The config blob and the manifest PUT are not layers and do not
/// produce progress events, so <see cref="TotalLayers"/> reflects exactly the number of layers (base + built).
/// </para>
/// </remarks>
public readonly record struct LayerPushProgress
{
    /// <summary>The 1-based position of this layer within the push, in the range 1..<see cref="TotalLayers"/>.</summary>
    public int LayerIndex { get; init; }

    /// <summary>The total number of layers in this push (base layers plus layers built by this library).</summary>
    public int TotalLayers { get; init; }

    /// <summary>The layer's <c>sha256:...</c> digest — its stable identity across all events for this layer.</summary>
    public string Digest { get; init; }

    /// <summary>
    /// The number of bytes of this layer uploaded so far. 0 at <see cref="LayerPushPhase.Started"/>, rising during
    /// <see cref="LayerPushPhase.Uploading"/>, and equal to <see cref="LayerSizeBytes"/> at
    /// <see cref="LayerPushPhase.Completed"/> for an uploaded layer (0 for a skipped one).
    /// </summary>
    public long BytesPushed { get; init; }

    /// <summary>
    /// The total size of this layer, in bytes. Reported even for a skipped layer (so a UI can show "skipped —
    /// 37 MB already present" rather than "0 of 0 bytes").
    /// </summary>
    public long LayerSizeBytes { get; init; }

    /// <summary>
    /// <see langword="true"/> when the layer was not uploaded because the registry already has the blob. Such a
    /// layer is reported as a single <see cref="LayerPushPhase.Completed"/> event with <see cref="BytesPushed"/>
    /// of 0.
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>The lifecycle phase this event represents.</summary>
    public LayerPushPhase Phase { get; init; }

    /// <summary>Whether this layer is a base-image layer or a layer built by this library.</summary>
    public LayerKind Kind { get; init; }
}
