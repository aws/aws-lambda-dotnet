using System.Security.Cryptography;

namespace Amazon.OCI.Builder.Images;

/// <summary>
/// Assembles a derived image from a base image's config and manifest plus one or more overlaid
/// <see cref="Layer"/>s. It keeps the config's <c>rootfs.diff_ids</c>/<c>history</c> and the manifest's
/// <c>layers</c> in lockstep, applies config customizations (entrypoint, env, etc.), and produces the final
/// config and manifest bytes together with their digests.
/// </summary>
/// <remarks>
/// <para>
/// This type performs <em>only</em> the in-memory config/manifest manipulation — the "overlay a layer" step. It
/// does not fetch the base image or push the result; those are the registry layer's job. A caller fetches the
/// base manifest and config, constructs an <see cref="ImageBuilder"/> from them, overlays layers, then hands the
/// built artifacts to the registry client for upload.
/// </para>
/// <para>
/// The output preserves the base image's schema family: if the base manifest is a Docker V2 manifest, the derived
/// manifest and the layer descriptors use the Docker media types; otherwise OCI media types are used. This avoids
/// producing a manifest whose media types are inconsistent with its config, which some registries reject.
/// </para>
/// </remarks>
public sealed class ImageBuilder
{
    private readonly ImageConfig _config;
    private readonly Manifest _manifest;
    private readonly bool _isDockerSchema;

    private ImageBuilder(ImageConfig config, Manifest manifest, bool isDockerSchema)
    {
        _config = config;
        _manifest = manifest;
        _isDockerSchema = isDockerSchema;
    }

    /// <summary>The media type to use for layers overlaid onto this image (matching the base image's schema).</summary>
    public string LayerMediaType => _isDockerSchema ? MediaTypes.DockerLayerGzip : MediaTypes.OciLayerGzipV1;

    /// <summary>The config blob media type for this image's schema.</summary>
    public string ConfigMediaType => _isDockerSchema ? MediaTypes.DockerImageConfigV1 : MediaTypes.OciImageConfigV1;

    /// <summary>The manifest media type for this image's schema.</summary>
    public string ManifestMediaType => _isDockerSchema ? MediaTypes.DockerManifestV2 : MediaTypes.OciManifestV1;

    /// <summary>The config being assembled, exposed for customization (entrypoint, env, labels, ...).</summary>
    public ImageConfig Config => _config;

    /// <summary>
    /// Creates a builder from an already-fetched base image's config and manifest.
    /// </summary>
    /// <param name="baseConfigJson">The base image's config blob JSON.</param>
    /// <param name="baseManifestJson">The base image's single-platform manifest JSON (not an index).</param>
    /// <param name="baseManifestMediaType">
    /// The media type the registry reported for the base manifest. Used to decide whether the derived image uses
    /// Docker or OCI media types. When null, OCI is assumed.
    /// </param>
    /// <exception cref="ArgumentException">The config or manifest JSON is missing or invalid.</exception>
    public static ImageBuilder FromBaseImage(string baseConfigJson, string baseManifestJson, string? baseManifestMediaType)
    {
        ImageConfig config = ImageConfig.Parse(baseConfigJson);
        Manifest manifest = Manifest.Parse(baseManifestJson);

        bool isDocker = IsDockerSchema(baseManifestMediaType, manifest.MediaType);
        return new ImageBuilder(config, manifest, isDocker);
    }

    /// <summary>
    /// Overlays a built <paramref name="layer"/> onto the image: appends its descriptor to the manifest's
    /// <c>layers</c> and its <c>diff_id</c> (plus a history entry) to the config. The layer's media type is
    /// coerced to the image's schema so the manifest stays internally consistent.
    /// </summary>
    /// <param name="layer">A layer produced by <see cref="Layer.CreateAsync"/>.</param>
    /// <param name="createdBy">Optional free-text description recorded in the config history for this layer.</param>
    public void AddLayer(Layer layer, string? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(layer);

        // Keep the layer descriptor's media type aligned with the image schema rather than trusting whatever the
        // layer was built with, so a layer built as OCI can still be overlaid onto a Docker-schema base and vice
        // versa without producing a mixed-schema manifest.
        _manifest.Layers.Add(new Descriptor
        {
            MediaType = LayerMediaType,
            Size = layer.Size,
            Digest = layer.Digest,
        });

        _config.AddLayer(layer.DiffId, createdBy);
    }

    /// <summary>
    /// Finalizes the image: serializes the (mutated) config, points the manifest's <c>config</c> descriptor at the
    /// fresh config digest, sets the manifest media type to the image schema, and serializes the manifest. The
    /// returned <see cref="BuiltImage"/> carries everything the registry layer needs to upload the image.
    /// </summary>
    public BuiltImage Build()
    {
        // 1. Serialize the config and compute its digest; the manifest must reference these exact bytes.
        byte[] configBytes = _config.ToJsonBytes();
        string configDigest = ComputeDigest(configBytes);

        _manifest.Config = new Descriptor
        {
            MediaType = ConfigMediaType,
            Size = configBytes.LongLength,
            Digest = configDigest,
        };
        _manifest.MediaType = ManifestMediaType;

        // 2. Serialize the manifest and compute the image digest over those bytes.
        byte[] manifestBytes = _manifest.ToJsonBytes();
        string manifestDigest = ComputeDigest(manifestBytes);

        return new BuiltImage(
            configBytes,
            configDigest,
            ConfigMediaType,
            manifestBytes,
            manifestDigest,
            ManifestMediaType);
    }

    /// <summary>
    /// Decides whether the image should be treated as Docker-schema (vs OCI). The reported manifest media type is
    /// the strongest signal; the manifest's own <c>mediaType</c> is next; the config's media type field is a last
    /// resort. Anything not clearly Docker defaults to OCI.
    /// </summary>
    private static bool IsDockerSchema(string? reportedManifestMediaType, string? manifestMediaType)
    {
        foreach (string? candidate in new[] { reportedManifestMediaType, manifestMediaType })
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }
            if (candidate.StartsWith("application/vnd.docker.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (candidate.StartsWith("application/vnd.oci.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return false;
    }

    private static string ComputeDigest(byte[] data)
    {
        byte[] hash = SHA256.HashData(data);
        // Convert.ToHexStringLower exists only on net9.0+, so lower-case explicitly for net8.0 compatibility.
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// The finished image artifacts produced by <see cref="ImageBuilder.Build"/>: the config and manifest bytes with
/// their digests and media types, ready for the registry layer to upload.
/// </summary>
/// <param name="ConfigBytes">The canonical config blob bytes to upload.</param>
/// <param name="ConfigDigest">The <c>sha256:...</c> digest of <paramref name="ConfigBytes"/>.</param>
/// <param name="ConfigMediaType">The config blob media type.</param>
/// <param name="ManifestBytes">The canonical manifest bytes to upload (and PUT under the target tag).</param>
/// <param name="ManifestDigest">The <c>sha256:...</c> digest of <paramref name="ManifestBytes"/> (the image digest).</param>
/// <param name="ManifestMediaType">The manifest media type to send as the <c>Content-Type</c> on upload.</param>
public sealed record BuiltImage(
    byte[] ConfigBytes,
    string ConfigDigest,
    string ConfigMediaType,
    byte[] ManifestBytes,
    string ManifestDigest,
    string ManifestMediaType);
