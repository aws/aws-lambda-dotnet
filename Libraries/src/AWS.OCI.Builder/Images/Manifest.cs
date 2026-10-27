using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.OCI.Builder.Images;

/// <summary>
/// An image manifest (OCI image manifest or Docker V2 Schema 2): the single-platform document that references the
/// image <see cref="Config"/> blob and the ordered set of <see cref="Layers"/> that make up the image.
/// </summary>
/// <remarks>
/// The manifest is serialized compactly and its property order is fixed (schema version, media type, config,
/// layers), because the image's digest/tag is computed over these exact bytes.
/// </remarks>
public sealed class Manifest
{
    /// <summary>The manifest schema version. Always <c>2</c> for OCI and Docker V2 Schema 2.</summary>
    [JsonPropertyName("schemaVersion")]
    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; set; } = 2;

    /// <summary>
    /// The manifest media type (for example <see cref="MediaTypes.OciManifestV1"/> or
    /// <see cref="MediaTypes.DockerManifestV2"/>). Some older Docker manifests omit this; it is written when set.
    /// </summary>
    [JsonPropertyName("mediaType")]
    [JsonPropertyOrder(1)]
    public string? MediaType { get; set; }

    /// <summary>The descriptor of the image config blob.</summary>
    [JsonPropertyName("config")]
    [JsonPropertyOrder(2)]
    public Descriptor Config { get; set; } = null!;

    /// <summary>The ordered layer descriptors (base layers first, then any overlaid layers).</summary>
    [JsonPropertyName("layers")]
    [JsonPropertyOrder(3)]
    public List<Descriptor> Layers { get; set; } = new();

    /// <summary>Parses an image manifest document.</summary>
    /// <exception cref="ArgumentException">The JSON is null/empty or cannot be parsed as a manifest.</exception>
    public static Manifest Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("The manifest JSON must not be null or empty.", nameof(json));
        }

        try
        {
            return JsonSerializer.Deserialize(json, OciJsonContext.ManifestInfo)
                   ?? throw new ArgumentException("The manifest JSON deserialized to null.", nameof(json));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The manifest is not valid JSON.", nameof(json), ex);
        }
    }

    /// <summary>Serializes the manifest to its canonical, compact UTF-8 bytes (what the image digest is computed over).</summary>
    public byte[] ToJsonBytes() => JsonSerializer.SerializeToUtf8Bytes(this, OciJsonContext.ManifestInfo);

    /// <summary>Serializes the manifest to its canonical, compact JSON string.</summary>
    public string ToJsonString() => JsonSerializer.Serialize(this, OciJsonContext.ManifestInfo);
}

/// <summary>
/// An image index / manifest list: a multi-platform document whose <see cref="Manifests"/> each point at a
/// single-platform <see cref="Manifest"/> for a specific OS/architecture. Base images are frequently published as
/// an index, so the builder must select the right per-platform manifest from it.
/// </summary>
public sealed class ImageIndex
{
    /// <summary>The schema version (always <c>2</c>).</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 2;

    /// <summary>The index media type (OCI image index or Docker manifest list).</summary>
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    /// <summary>The per-platform manifest descriptors.</summary>
    [JsonPropertyName("manifests")]
    public List<PlatformManifestDescriptor> Manifests { get; set; } = new();

    /// <summary>Parses an image index / manifest list document.</summary>
    /// <exception cref="ArgumentException">The JSON is null/empty or cannot be parsed as an index.</exception>
    public static ImageIndex Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("The image index JSON must not be null or empty.", nameof(json));
        }

        try
        {
            return JsonSerializer.Deserialize(json, OciJsonContext.ImageIndexInfo)
                   ?? throw new ArgumentException("The image index JSON deserialized to null.", nameof(json));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The image index is not valid JSON.", nameof(json), ex);
        }
    }

    /// <summary>
    /// Selects the manifest descriptor matching <paramref name="os"/> and <paramref name="architecture"/>,
    /// ignoring descriptors that carry no platform or name an "unknown" platform (attestation entries).
    /// </summary>
    /// <returns>The matching descriptor, or <see langword="null"/> when the index has no such platform.</returns>
    public PlatformManifestDescriptor? FindManifest(string os, string architecture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(os);
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);

        return Manifests.FirstOrDefault(m =>
            m.Platform is { } p &&
            string.Equals(p.Os, os, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Architecture, architecture, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>A manifest descriptor within an <see cref="ImageIndex"/>, carrying the target platform.</summary>
public sealed class PlatformManifestDescriptor
{
    /// <summary>The media type of the referenced manifest.</summary>
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = null!;

    /// <summary>The size, in bytes, of the referenced manifest.</summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>The <c>sha256:...</c> digest of the referenced manifest.</summary>
    [JsonPropertyName("digest")]
    public string Digest { get; set; } = null!;

    /// <summary>The platform this manifest targets, if declared.</summary>
    [JsonPropertyName("platform")]
    public Platform? Platform { get; set; }
}

/// <summary>An OS/architecture platform descriptor within an image index.</summary>
public sealed class Platform
{
    /// <summary>The operating system, for example <c>linux</c>.</summary>
    [JsonPropertyName("os")]
    public string Os { get; set; } = null!;

    /// <summary>The CPU architecture, for example <c>amd64</c> or <c>arm64</c>.</summary>
    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = null!;

    /// <summary>An optional architecture variant, for example <c>v8</c> for <c>arm64</c>.</summary>
    [JsonPropertyName("variant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Variant { get; set; }
}
