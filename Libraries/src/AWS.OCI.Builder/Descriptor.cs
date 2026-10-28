using System.Text.Json.Serialization;

namespace AWS.OCI.Builder;

/// <summary>
/// An OCI content descriptor: a typed, content-addressable reference to a blob
/// (a layer, a config, or a manifest) by its media type, size, and digest.
/// See the OCI Image Specification "descriptor" definition.
/// </summary>
public sealed record Descriptor
{
    /// <summary>The media type of the referenced content (for example an image config or a gzipped layer).</summary>
    [JsonPropertyName("mediaType")]
    public required string MediaType { get; init; }

    /// <summary>The size, in bytes, of the referenced content.</summary>
    [JsonPropertyName("size")]
    public required long Size { get; init; }

    /// <summary>
    /// The digest of the referenced content, in "sha256:&lt;hex&gt;" form. For a layer this is the
    /// digest of the <em>compressed</em> (gzipped) tarball — the value used to address the blob in the registry.
    /// </summary>
    [JsonPropertyName("digest")]
    public required string Digest { get; init; }

    /// <summary>Optional alternate download locations for the content.</summary>
    [JsonPropertyName("urls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Urls { get; init; }
}
