namespace Amazon.OCI.Builder;

/// <summary>
/// Well-known media type strings used by the OCI Image Specification and the legacy
/// Docker Image Manifest V2 Schema 2. A base image may use either family; the builder
/// preserves the base image's family when producing the output manifest.
/// </summary>
public static class MediaTypes
{
    // --- OCI (application/vnd.oci.*) ---

    /// <summary>OCI image manifest (a single-platform image).</summary>
    public const string OciManifestV1 = "application/vnd.oci.image.manifest.v1+json";

    /// <summary>OCI image index (a multi-platform manifest list).</summary>
    public const string OciImageIndexV1 = "application/vnd.oci.image.index.v1+json";

    /// <summary>OCI image config.</summary>
    public const string OciImageConfigV1 = "application/vnd.oci.image.config.v1+json";

    /// <summary>OCI gzipped layer.</summary>
    public const string OciLayerGzipV1 = "application/vnd.oci.image.layer.v1.tar+gzip";

    // --- Docker Image Manifest V2 Schema 2 (application/vnd.docker.*) ---

    /// <summary>Docker image manifest (a single-platform image).</summary>
    public const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";

    /// <summary>Docker manifest list (a multi-platform manifest list).</summary>
    public const string DockerManifestListV2 = "application/vnd.docker.distribution.manifest.list.v2+json";

    /// <summary>Docker image config.</summary>
    public const string DockerImageConfigV1 = "application/vnd.docker.container.image.v1+json";

    /// <summary>Docker gzipped layer.</summary>
    public const string DockerLayerGzip = "application/vnd.docker.image.rootfs.diff.tar.gzip";
}
