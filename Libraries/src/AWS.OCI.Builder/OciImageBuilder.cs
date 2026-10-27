using Amazon.OCI.Builder.Images;
using Amazon.OCI.Builder.Registry;

namespace Amazon.OCI.Builder;

/// <summary>
/// The programmatic entry point for building an OCI container image from a base image plus local files and
/// pushing it to Amazon ECR — no Docker daemon required.
/// </summary>
/// <remarks>
/// <para>
/// The flow mirrors what the .NET SDK's container tooling does internally, specialized for ECR as the push target:
/// </para>
/// <list type="number">
///   <item>Pull the base image's manifest from its source registry (selecting the platform from a manifest list
///     when the base is multi-platform), then its config blob. Base layer <em>blobs</em> are not downloaded unless
///     they must be copied to ECR.</item>
///   <item>Build one gzipped-tar layer per <see cref="AddLayerAsync"/> call, overlaying the local files at
///     their target container paths.</item>
///   <item>Apply any config customizations (entrypoint, env, working directory, ...).</item>
///   <item>Copy the base layers to the target ECR repository, upload the new layer(s) and the rewritten config,
///     then PUT the manifest under the destination tag.</item>
/// </list>
/// <para>
/// Typical use:
/// </para>
/// <code>
/// var builder = await OciImageBuilder.FromBaseImageAsync(
///     "mcr.microsoft.com/dotnet/aspnet:8.0", platform: "linux/amd64");
///
/// await builder.AddLayerAsync(new FileManifest { { "bin/Release/net8.0/publish", "/app" } });
/// builder.Config.SetWorkingDirectory("/app");
/// builder.Config.SetEntrypoint("dotnet", "/app/MyApp.dll");
///
/// // Obtain an ECR authorization token (e.g. via AmazonECRClient.GetAuthorizationTokenAsync) and pass it in.
/// await builder.PushToEcrAsync("123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp:latest", authorizationToken);
/// </code>
/// <para>This type is not thread-safe; use one instance per image build.</para>
/// </remarks>
public sealed class OciImageBuilder : IDisposable
{
    private readonly ImageReference _baseReference;
    private readonly RegistryClient _sourceClient;
    private readonly ImageBuilder _imageBuilder;
    private readonly IReadOnlyList<Descriptor> _baseLayers;
    private readonly List<PendingLayer> _pendingLayers = new();
    private bool _disposed;

    /// <summary>
    /// Factory that produces the registry client used to push to the target. Defaults to an ECR-authenticating
    /// client built from the push options; replaced in tests to inject a stubbed transport. The default ignores
    /// the credential-source argument because <see cref="PushToEcrAsync"/> builds the ECR source itself.
    /// </summary>
    private Func<string, IRegistryCredentialSource, RegistryClient>? _targetClientFactory;

    /// <summary>The HTTP timeout for the push client, resolved from <see cref="EcrPushOptions.UploadTimeout"/> at
    /// push time and read by the default target-client factory.</summary>
    private TimeSpan _uploadTimeout = Timeout.InfiniteTimeSpan;

    /// <summary>Creates the push-target registry client, honoring the resolved upload timeout. A test override
    /// (<see cref="UseTargetClientFactoryForTest"/>) takes precedence when set.</summary>
    private RegistryClient CreateTargetClient(string host, IRegistryCredentialSource credentialSource)
        => _targetClientFactory is { } factory
            ? factory(host, credentialSource)
            : RegistryClient.Create(host, credentialSource, _uploadTimeout);

    private OciImageBuilder(
        ImageReference baseReference,
        RegistryClient sourceClient,
        ImageBuilder imageBuilder,
        IReadOnlyList<Descriptor> baseLayers)
    {
        _baseReference = baseReference;
        _sourceClient = sourceClient;
        _imageBuilder = imageBuilder;
        _baseLayers = baseLayers;
    }

    /// <summary>
    /// Test-only factory: builds an instance directly from pre-fetched base data and an injected source client,
    /// bypassing the live base-image pull. Combine with <see cref="UseTargetClientFactoryForTest"/> to exercise
    /// the full push flow against stubbed transports.
    /// </summary>
    internal static OciImageBuilder CreateForTest(
        ImageReference baseReference,
        RegistryClient sourceClient,
        ImageBuilder imageBuilder,
        IReadOnlyList<Descriptor> baseLayers)
        => new(baseReference, sourceClient, imageBuilder, baseLayers);

    /// <summary>Test-only: overrides how the push-target registry client is created.</summary>
    internal void UseTargetClientFactoryForTest(Func<string, IRegistryCredentialSource, RegistryClient> factory)
        => _targetClientFactory = factory;

    /// <summary>The config of the image being built, exposed for customization (entrypoint, env, labels, ...).</summary>
    public ImageConfig Config => _imageBuilder.Config;

    /// <summary>
    /// Fetches a base image and prepares a builder to overlay layers onto it.
    /// </summary>
    /// <param name="baseImage">
    /// The base image reference, for example <c>mcr.microsoft.com/dotnet/aspnet:8.0</c> or
    /// <c>ubuntu:22.04</c>. Public registries (anonymous or Bearer-token pull) are supported.
    /// </param>
    /// <param name="platform">
    /// The target platform as <c>os/architecture</c> (for example <c>linux/amd64</c> or <c>linux/arm64</c>), used
    /// to select the right manifest when the base image is published as a multi-platform manifest list. Defaults to
    /// <c>linux/amd64</c>.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="ArgumentException">The base reference or platform string is malformed.</exception>
    /// <exception cref="RegistryException">The base image or the requested platform could not be fetched.</exception>
    public static async Task<OciImageBuilder> FromBaseImageAsync(
        string baseImage,
        string platform = "linux/amd64",
        CancellationToken cancellationToken = default)
    {
        ImageReference baseReference = ImageReference.Parse(baseImage);
        (string os, string architecture) = ParsePlatform(platform);

        // Public base images pull anonymously (the handshake still performs a Bearer-token exchange when the
        // registry demands one, e.g. Docker Hub; the token is obtained without credentials).
        RegistryClient sourceClient = RegistryClient.Create(baseReference.Registry, new AnonymousRegistryCredentialSource());

        try
        {
            // 1. Fetch the base reference. It may be a single-platform manifest or a manifest list / image index.
            ManifestResponse top = await sourceClient
                .GetManifestAsync(baseReference.Repository, baseReference.Reference, cancellationToken)
                .ConfigureAwait(false);

            // Verify the top-level document's integrity. When the caller pinned a digest (repo@sha256:...), that
            // digest is authoritative and must match. Otherwise fall back to the registry-reported
            // Docker-Content-Digest when present — cheap defense-in-depth against a cache/proxy corrupting the
            // manifest in transit. (A tag fetch from a registry that omits the header is left unverified here;
            // the per-platform manifest, config, and layers it points at are all verified by digest below.)
            string? topExpectedDigest = baseReference.ReferenceIsDigest ? baseReference.Reference : top.Digest;
            if (topExpectedDigest is not null)
            {
                RegistryClient.VerifyDigest("base image manifest", topExpectedDigest, top.RawBytes);
            }

            ManifestResponse platformManifest = await ResolvePlatformManifestAsync(
                sourceClient, baseReference, top, os, architecture, cancellationToken).ConfigureAwait(false);

            // 2. Fetch the config blob referenced by the single-platform manifest, verifying its digest.
            Manifest manifest = Manifest.Parse(platformManifest.Json);
            if (manifest.Config is null)
            {
                throw new RegistryException($"The base manifest for '{baseImage}' has no config descriptor.");
            }

            byte[] configBytes = await sourceClient
                .GetVerifiedBlobBytesAsync(baseReference.Repository, manifest.Config.Digest, "base image config", cancellationToken)
                .ConfigureAwait(false);
            string configJson = System.Text.Encoding.UTF8.GetString(configBytes);

            // Validate the platform against the config we actually fetched. ResolvePlatformManifestAsync selects
            // the right entry from a manifest LIST, but a single-platform tag/digest is returned without any
            // platform comparison — so requesting linux/arm64 from an amd64-only image would otherwise succeed
            // and silently push an amd64 image. The config's os/architecture are authoritative, so check them
            // here and fail loudly on a mismatch rather than producing a wrong-platform image.
            ImageConfig parsedConfig = ImageConfig.Parse(configJson);
            if (parsedConfig.Os is { } configOs && !configOs.Equals(os, StringComparison.OrdinalIgnoreCase))
            {
                throw new RegistryException(
                    $"The base image '{baseImage}' is for OS '{configOs}', but '{os}' was requested. " +
                    "Pass a matching platform, or a base image/tag that provides the requested platform.");
            }
            if (parsedConfig.Architecture is { } configArch && !configArch.Equals(architecture, StringComparison.OrdinalIgnoreCase))
            {
                throw new RegistryException(
                    $"The base image '{baseImage}' is for architecture '{configArch}', but '{architecture}' was " +
                    "requested. Pass a matching platform, or a base image/tag that provides the requested platform.");
            }

            // 3. Prepare the in-memory overlay builder, preserving the base image's schema (OCI vs Docker).
            ImageBuilder imageBuilder = ImageBuilder.FromBaseImage(configJson, platformManifest.Json, platformManifest.MediaType);

            // Capture the base image's layer descriptors now; PushToEcrAsync copies these to ECR before uploading the
            // new layer(s). They are a snapshot of the base manifest, distinct from layers added by this build.
            IReadOnlyList<Descriptor> baseLayers = manifest.Layers.ToArray();

            return new OciImageBuilder(baseReference, sourceClient, imageBuilder, baseLayers);
        }
        catch
        {
            sourceClient.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Adds the files described by <paramref name="files"/> as a new image layer overlaid on top of the base
    /// (and any previously-added) layers. Each call produces one layer. Call this before <see cref="PushToEcrAsync"/>.
    /// </summary>
    /// <param name="files">The local-to-container path mappings that populate the layer.</param>
    /// <param name="createdBy">Optional free-text description recorded in the image history for this layer.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="ArgumentException">The manifest is empty or an entry has an invalid container path.</exception>
    /// <exception cref="FileNotFoundException">A mapped local file does not exist.</exception>
    public async Task AddLayerAsync(FileManifest files, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(files);

        // Build the layer with the media type matching the image schema so the manifest stays consistent.
        Layer layer = await Layer.CreateAsync(files, _imageBuilder.LayerMediaType, cancellationToken).ConfigureAwait(false);

        // Spill the compressed layer to a temp file FIRST, so it is not retained in memory for the builder's
        // lifetime. The file is created with owner-only permissions (see SpillFile) so other local users cannot
        // read a private layer from the shared temp directory. The builder owns the file and deletes it on Dispose.
        string layerFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aws-oci-built-layer-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var file = Registry.SpillFile.CreateWrite(layerFile))
            {
                await file.WriteAsync(layer.GzippedTarBytes, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            TryDeleteFile(layerFile);
            throw;
        }

        // Only now that the blob is safely on disk do we mutate the image builder (adding the layer's descriptor
        // and diff_id) and record the pending layer. Doing this before the spill could, on a failed/cancelled
        // write, leave the manifest referencing a layer whose blob never materializes — a later push would then
        // reference a missing blob, and a retry could duplicate the layer entry.
        _imageBuilder.AddLayer(layer, createdBy);
        _pendingLayers.Add(new PendingLayer(layer.Digest, layerFile, layer.GzippedTarBytes.LongLength));
    }

    /// <summary>
    /// Pushes the assembled image to the given Amazon ECR reference, authenticating with an ECR authorization
    /// token the caller has already obtained. The target repository must already exist in ECR (this library does
    /// not create repositories). The region is taken from the ECR host in <paramref name="targetImage"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is named for its ECR-specific behavior: it expects <paramref name="authorizationToken"/> to be
    /// the token returned by the ECR <c>GetAuthorizationToken</c> API — a base64-encoded <c>AWS:&lt;password&gt;</c>
    /// string — which the library decodes into the <c>Basic</c> username/password the registry handshake uses.
    /// </para>
    /// <para>
    /// The library deliberately does <em>not</em> depend on the AWS SDK: obtaining the token (and refreshing it if
    /// the push outlives its ~12-hour validity) is the caller's responsibility. A typical caller holds an
    /// <c>AmazonECRClient</c> (or AWS Tools for PowerShell) already and passes the token straight through, e.g.:
    /// </para>
    /// <code>
    /// var auth = await ecr.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
    /// string token = auth.AuthorizationData[0].AuthorizationToken;   // base64("AWS:&lt;password&gt;")
    /// string digest = await builder.PushToEcrAsync(targetImage, token);
    /// </code>
    /// </remarks>
    /// <param name="targetImage">
    /// The destination ECR reference, for example
    /// <c>123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp:latest</c>.
    /// </param>
    /// <param name="authorizationToken">
    /// The ECR authorization token — the base64-encoded <c>AWS:&lt;password&gt;</c> string from the ECR
    /// <c>GetAuthorizationToken</c> API's <c>AuthorizationData.AuthorizationToken</c>.
    /// </param>
    /// <param name="options">Optional push settings (for example the in-memory layer threshold).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The pushed image's manifest digest (<c>sha256:...</c>).</returns>
    /// <exception cref="ArgumentException">
    /// The target reference is not a valid ECR reference, the authorization token is missing or malformed, or an
    /// option value is invalid.
    /// </exception>
    /// <exception cref="RegistryException">A registry operation failed.</exception>
    public async Task<string> PushToEcrAsync(
        string targetImage,
        string authorizationToken,
        EcrPushOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ImageReference target = ImageReference.Parse(targetImage);
        if (!target.IsEcr)
        {
            throw new ArgumentException(
                $"The push target '{targetImage}' is not an Amazon ECR reference. This library only pushes to ECR " +
                "(e.g. 123456789012.dkr.ecr.us-west-2.amazonaws.com/repo:tag).",
                nameof(targetImage));
        }

        long maxInMemoryLayerBytes = options?.MaxInMemoryLayerBytes ?? EcrPushOptions.DefaultMaxInMemoryLayerBytes;
        if (maxInMemoryLayerBytes < 0)
        {
            throw new ArgumentException(
                $"{nameof(EcrPushOptions.MaxInMemoryLayerBytes)} must be non-negative (was {maxInMemoryLayerBytes}).",
                nameof(options));
        }

        TimeSpan uploadTimeout = options?.UploadTimeout ?? Timeout.InfiniteTimeSpan;
        if (uploadTimeout != Timeout.InfiniteTimeSpan && uploadTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"{nameof(EcrPushOptions.UploadTimeout)} must be positive or Timeout.InfiniteTimeSpan (was {uploadTimeout}).",
                nameof(options));
        }
        _uploadTimeout = uploadTimeout;

        // Decode the ECR authorization token into the Basic username/password the registry handshake expects.
        (string username, string password) = DecodeEcrAuthorizationToken(authorizationToken);
        var credentialSource = new StaticRegistryCredentialSource(username, password);

        IProgress<LayerPushProgress>? progress = options?.Progress;

        using RegistryClient targetClient = CreateTargetClient(target.Registry, credentialSource);

        // 1. Finalize the config + manifest bytes and their digests.
        BuiltImage built = _imageBuilder.Build();

        // Assign each layer its stable identity (index, total, digest, size, kind) BEFORE any upload begins. Base
        // layers (in manifest order) come first, then the locally-built layers (in add order). Fixing identity up
        // front is what makes the progress contract survive a future parallel push: an event always carries its
        // layer's own index/digest, never a position derived from the order work happens to finish in.
        int totalLayers = _baseLayers.Count + _pendingLayers.Count;

        // 2. Copy the base image's layers from the source registry to the ECR target. Cross-host, so each base
        //    layer that ECR does not already have is streamed from the source and uploaded. Base layers take the
        //    first index slots; the call returns the next free index for the locally-built layers.
        int nextIndex = await CopyBaseLayersAsync(target.Repository, targetClient, maxInMemoryLayerBytes, progress, totalLayers, cancellationToken)
            .ConfigureAwait(false);

        // 3. Upload the new layer blob(s) produced locally. Each was spilled to a temp file by AddLayerAsync, so
        //    it is streamed from disk here rather than held in memory.
        foreach (PendingLayer pending in _pendingLayers)
        {
            int layerIndex = nextIndex++;
            long size = pending.Size;

            if (await targetClient.BlobExistsAsync(target.Repository, pending.Digest, cancellationToken).ConfigureAwait(false))
            {
                ReportSkipped(progress, layerIndex, totalLayers, pending.Digest, size, LayerKind.BuiltLayer);
                continue;
            }

            ReportStarted(progress, layerIndex, totalLayers, pending.Digest, size, LayerKind.BuiltLayer);

            await using (var layerStream = new FileStream(
                pending.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true))
            {
                await targetClient.UploadBlobAsync(
                        target.Repository,
                        pending.Digest,
                        layerStream,
                        cancellationToken,
                        onBytesSent: sent => ReportUploading(progress, layerIndex, totalLayers, pending.Digest, sent, size, LayerKind.BuiltLayer),
                        contentLength: size)
                    .ConfigureAwait(false);
            }

            ReportCompleted(progress, layerIndex, totalLayers, pending.Digest, size, LayerKind.BuiltLayer);
        }

        // 4. Upload the config blob.
        using (var configStream = new MemoryStream(built.ConfigBytes, writable: false))
        {
            await targetClient.UploadBlobAsync(
                    target.Repository, built.ConfigDigest, configStream, cancellationToken, contentLength: built.ConfigBytes.LongLength)
                .ConfigureAwait(false);
        }

        // 5. PUT the manifest under the destination tag/digest. All referenced blobs are now present.
        string manifestJson = System.Text.Encoding.UTF8.GetString(built.ManifestBytes);
        await targetClient.PutManifestAsync(target.Repository, target.Reference, manifestJson, built.ManifestMediaType, cancellationToken)
            .ConfigureAwait(false);

        return built.ManifestDigest;
    }

    /// <summary>
    /// Copies every base-image layer from the source registry into the target ECR repository, skipping any blob
    /// ECR already has. Base layers come from a different host than ECR, so cross-repo mount cannot be used — the
    /// blob is streamed from the source and re-uploaded.
    /// </summary>
    /// <remarks>
    /// A layer whose declared size is at or below <paramref name="maxInMemoryLayerBytes"/> is buffered in memory;
    /// a larger one is streamed through a temporary file on disk to bound peak memory. Both paths verify the
    /// layer's digest before upload (the disk path verifies twice — see
    /// <see cref="RegistryClient.GetVerifiedBlobToFileAsync"/>), so data integrity holds regardless of which path
    /// a layer takes.
    /// </remarks>
    private async Task<int> CopyBaseLayersAsync(
        string targetRepository,
        RegistryClient targetClient,
        long maxInMemoryLayerBytes,
        IProgress<LayerPushProgress>? progress,
        int totalLayers,
        CancellationToken cancellationToken)
    {
        // Base layers occupy the first index slots (1..baseCount). The local counter advances per layer and its
        // final value — the next free index — is returned for the locally-built layers that follow.
        int index = 1;
        foreach (Descriptor layer in _baseLayers)
        {
            int layerIndex = index++;
            long size = layer.Size;

            if (await targetClient.BlobExistsAsync(targetRepository, layer.Digest, cancellationToken).ConfigureAwait(false))
            {
                ReportSkipped(progress, layerIndex, totalLayers, layer.Digest, size, LayerKind.BaseLayer);
                continue;
            }

            ReportStarted(progress, layerIndex, totalLayers, layer.Digest, size, LayerKind.BaseLayer);

            if (layer.Size > maxInMemoryLayerBytes)
            {
                // Large layer: stream to a temp file (verified twice) and upload from disk, bounding memory.
                await using VerifiedBlobFile spilled = await _sourceClient
                    .GetVerifiedBlobToFileAsync(_baseReference.Repository, layer.Digest, "base layer", cancellationToken)
                    .ConfigureAwait(false);

                await targetClient.UploadBlobAsync(
                        targetRepository,
                        layer.Digest,
                        spilled.Stream,
                        cancellationToken,
                        onBytesSent: sent => ReportUploading(progress, layerIndex, totalLayers, layer.Digest, sent, size, LayerKind.BaseLayer),
                        contentLength: size)
                    .ConfigureAwait(false);
            }
            else
            {
                // Small layer: download, verify, and re-upload from memory.
                byte[] blob = await _sourceClient
                    .GetVerifiedBlobBytesAsync(_baseReference.Repository, layer.Digest, "base layer", cancellationToken)
                    .ConfigureAwait(false);

                using var upload = new MemoryStream(blob, writable: false);
                await targetClient.UploadBlobAsync(
                        targetRepository,
                        layer.Digest,
                        upload,
                        cancellationToken,
                        onBytesSent: sent => ReportUploading(progress, layerIndex, totalLayers, layer.Digest, sent, size, LayerKind.BaseLayer),
                        contentLength: blob.LongLength)
                    .ConfigureAwait(false);
            }

            ReportCompleted(progress, layerIndex, totalLayers, layer.Digest, size, LayerKind.BaseLayer);
        }

        return index;
    }

    // ---- Progress reporting helpers ------------------------------------------------------------
    // Each helper builds one LayerPushProgress event and hands it to the sink (when one is configured). Pulling
    // these out keeps the push methods readable and guarantees every event sets the same stable-identity fields.

    private static void ReportStarted(
        IProgress<LayerPushProgress>? progress, int index, int total, string digest, long size, LayerKind kind)
        => progress?.Report(new LayerPushProgress
        {
            LayerIndex = index,
            TotalLayers = total,
            Digest = digest,
            BytesPushed = 0,
            LayerSizeBytes = size,
            IsSkipped = false,
            Phase = LayerPushPhase.Started,
            Kind = kind,
        });

    private static void ReportUploading(
        IProgress<LayerPushProgress>? progress, int index, int total, string digest, long bytesPushed, long size, LayerKind kind)
        => progress?.Report(new LayerPushProgress
        {
            LayerIndex = index,
            TotalLayers = total,
            Digest = digest,
            BytesPushed = bytesPushed,
            LayerSizeBytes = size,
            IsSkipped = false,
            Phase = LayerPushPhase.Uploading,
            Kind = kind,
        });

    private static void ReportCompleted(
        IProgress<LayerPushProgress>? progress, int index, int total, string digest, long size, LayerKind kind)
        => progress?.Report(new LayerPushProgress
        {
            LayerIndex = index,
            TotalLayers = total,
            Digest = digest,
            BytesPushed = size,
            LayerSizeBytes = size,
            IsSkipped = false,
            Phase = LayerPushPhase.Completed,
            Kind = kind,
        });

    private static void ReportSkipped(
        IProgress<LayerPushProgress>? progress, int index, int total, string digest, long size, LayerKind kind)
        => progress?.Report(new LayerPushProgress
        {
            LayerIndex = index,
            TotalLayers = total,
            Digest = digest,
            BytesPushed = 0,
            LayerSizeBytes = size,
            IsSkipped = true,
            Phase = LayerPushPhase.Completed,
            Kind = kind,
        });

    private static async Task<ManifestResponse> ResolvePlatformManifestAsync(
        RegistryClient client,
        ImageReference baseReference,
        ManifestResponse top,
        string os,
        string architecture,
        CancellationToken cancellationToken)
    {
        // Single-platform manifest — use it directly.
        if (!IsIndexMediaType(top.MediaType))
        {
            return top;
        }

        // Manifest list / image index — pick the matching platform and fetch that manifest by digest, then verify
        // the returned bytes hash to the digest we requested it under.
        ImageIndex index = ImageIndex.Parse(top.Json);
        PlatformManifestDescriptor? selected = index.FindManifest(os, architecture)
            ?? throw new RegistryException(
                $"The base image '{baseReference}' has no manifest for platform '{os}/{architecture}'. " +
                $"Available: {DescribePlatforms(index)}.");

        ManifestResponse platformManifest =
            await client.GetManifestAsync(baseReference.Repository, selected.Digest, cancellationToken).ConfigureAwait(false);

        RegistryClient.VerifyDigest(
            $"platform manifest {os}/{architecture}",
            selected.Digest,
            platformManifest.RawBytes);

        return platformManifest;
    }

    private static string DescribePlatforms(ImageIndex index)
    {
        IEnumerable<string> platforms = index.Manifests
            .Where(m => m.Platform is not null && !string.Equals(m.Platform!.Os, "unknown", StringComparison.OrdinalIgnoreCase))
            .Select(m => $"{m.Platform!.Os}/{m.Platform.Architecture}");
        string joined = string.Join(", ", platforms);
        return joined.Length == 0 ? "(none)" : joined;
    }

    private static bool IsIndexMediaType(string mediaType)
        => string.Equals(mediaType, MediaTypes.OciImageIndexV1, StringComparison.OrdinalIgnoreCase)
           || string.Equals(mediaType, MediaTypes.DockerManifestListV2, StringComparison.OrdinalIgnoreCase);

    private static (string Os, string Architecture) ParsePlatform(string platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        string[] parts = platform.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                $"The platform '{platform}' must be in 'os/architecture' form, for example 'linux/amd64'.",
                nameof(platform));
        }
        return (parts[0], parts[1]);
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Decodes an ECR authorization token (base64 of <c>username:password</c>, where the username is <c>AWS</c>)
    /// into its username/password parts.
    /// </summary>
    /// <exception cref="ArgumentException">The token is null/empty, not valid base64, or not in the expected form.</exception>
    private static (string Username, string Password) DecodeEcrAuthorizationToken(string authorizationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationToken);

        byte[] decodedBytes;
        try
        {
            decodedBytes = Convert.FromBase64String(authorizationToken.Trim());
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(
                "The ECR authorization token is not valid base64. Pass the AuthorizationData.AuthorizationToken " +
                "value returned by the ECR GetAuthorizationToken API verbatim.",
                nameof(authorizationToken), ex);
        }

        string decoded = System.Text.Encoding.UTF8.GetString(decodedBytes);
        int separator = decoded.IndexOf(':');
        if (separator < 0)
        {
            throw new ArgumentException(
                "The ECR authorization token did not decode to the expected 'username:password' form.",
                nameof(authorizationToken));
        }

        return (decoded[..separator], decoded[(separator + 1)..]);
    }

    /// <summary>Disposes the source registry client and deletes any temp files holding spilled built layers.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        foreach (PendingLayer pending in _pendingLayers)
        {
            TryDeleteFile(pending.FilePath);
        }
        _pendingLayers.Clear();

        _sourceClient.Dispose();
    }

    /// <summary>Best-effort delete of a temp file; a leftover must never mask a more important error.</summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>
    /// A layer built locally during this session. The compressed blob is spilled to <see cref="FilePath"/> (not
    /// kept in memory) and streamed from there by <see cref="PushToEcrAsync"/>; the builder deletes the file on
    /// <see cref="Dispose"/>.
    /// </summary>
    private readonly record struct PendingLayer(string Digest, string FilePath, long Size);
}
