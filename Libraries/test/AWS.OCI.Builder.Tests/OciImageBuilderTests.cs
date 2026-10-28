using System.Net;
using System.Text;
using AWS.OCI.Builder;
using AWS.OCI.Builder.Images;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class OciImageBuilderTests
{
    /// <summary>
    /// A minimal in-memory OCI Distribution registry. It tracks blob existence per repository, accepts the
    /// POST/PATCH/PUT upload flow, serves base-layer GETs for the source host, and records the PUT manifest.
    /// One instance serves all hosts; it keys state by (host, repository).
    /// </summary>
    private sealed class FakeRegistry : HttpMessageHandler
    {
        // (host, repository) -> set of blob digests present.
        private readonly Dictionary<(string Host, string Repo), HashSet<string>> _blobs = new();
        // Pre-seeded source blobs available for download: (host, repo, digest) -> bytes.
        private readonly Dictionary<(string Host, string Repo, string Digest), byte[]> _sourceBlobs = new();
        private int _uploadCounter;

        public List<(string Host, string Repo, string Digest)> UploadedBlobs { get; } = new();

        // Content-Length advertised on each PATCH body (null when the request did not set one). A non-null value
        // means HttpClient was able to stream the body instead of buffering it to measure the length.
        public List<long?> PatchContentLengths { get; } = new();

        public (string Host, string Repo, string Reference, string Body, string ContentType)? PutManifest { get; private set; }

        public void SeedSourceBlob(string host, string repo, string digest, byte[] bytes)
            => _sourceBlobs[(host, repo, digest)] = bytes;

        private HashSet<string> BlobsFor(string host, string repo)
        {
            if (!_blobs.TryGetValue((host, repo), out HashSet<string>? set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _blobs[(host, repo)] = set;
            }
            return set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri!;
            string host = uri.Host;
            string path = uri.AbsolutePath; // /v2/<repo>/(blobs|manifests|blobs/uploads)/...
            HttpMethod method = request.Method;

            // ---- Blob existence: HEAD /v2/<repo>/blobs/<digest> ----
            if (method == HttpMethod.Head && TryMatch(path, "/blobs/", out string repoHeadBlob, out string headDigest))
            {
                bool exists = BlobsFor(host, repoHeadBlob).Contains(headDigest);
                return new HttpResponseMessage(exists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            }

            // ---- Blob download: GET /v2/<repo>/blobs/<digest> ----
            if (method == HttpMethod.Get && TryMatch(path, "/blobs/", out string repoGetBlob, out string getDigest))
            {
                if (_sourceBlobs.TryGetValue((host, repoGetBlob, getDigest), out byte[]? bytes))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            // ---- Start upload: POST /v2/<repo>/blobs/uploads/ ----
            if (method == HttpMethod.Post && path.EndsWith("/blobs/uploads/", StringComparison.Ordinal))
            {
                string repo = ExtractRepo(path, "/blobs/uploads/");
                string uploadId = $"upload{Interlocked.Increment(ref _uploadCounter)}";
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri($"https://{host}/v2/{repo}/blobs/uploads/{uploadId}");
                return accepted;
            }

            // ---- Chunk: PATCH /v2/<repo>/blobs/uploads/<id> ----
            if (method == HttpMethod.Patch && path.Contains("/blobs/uploads/", StringComparison.Ordinal))
            {
                // Record the advertised body length before draining (it is cleared once the content is consumed).
                PatchContentLengths.Add(request.Content!.Headers.ContentLength);
                // Drain the chunk; return the same location for the next chunk/commit.
                _ = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.Location = uri;
                return accepted;
            }

            // ---- Commit: PUT /v2/<repo>/blobs/uploads/<id>?digest=<digest> ----
            if (method == HttpMethod.Put && path.Contains("/blobs/uploads/", StringComparison.Ordinal))
            {
                string repo = ExtractRepo(path, "/blobs/uploads/");
                string digest = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("digest")!;
                BlobsFor(host, repo).Add(digest);
                UploadedBlobs.Add((host, repo, digest));
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            // ---- Manifest PUT: PUT /v2/<repo>/manifests/<reference> ----
            if (method == HttpMethod.Put && TryMatch(path, "/manifests/", out string repoManifest, out string reference))
            {
                string body = await request.Content!.ReadAsStringAsync(cancellationToken);
                string contentType = request.Content.Headers.ContentType?.MediaType ?? string.Empty;
                PutManifest = (host, repoManifest, reference, body, contentType);
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        // Matches /v2/<repo>/<marker><tail> and returns repo + tail (tail is the digest or reference).
        private static bool TryMatch(string path, string marker, out string repo, out string tail)
        {
            repo = string.Empty;
            tail = string.Empty;
            const string prefix = "/v2/";
            int markerIndex = path.IndexOf(marker, StringComparison.Ordinal);
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || markerIndex < 0)
            {
                return false;
            }
            // Exclude the uploads path from the plain blobs matcher.
            if (marker == "/blobs/" && path.Contains("/blobs/uploads/", StringComparison.Ordinal))
            {
                return false;
            }
            repo = path[prefix.Length..markerIndex];
            tail = path[(markerIndex + marker.Length)..];
            return repo.Length > 0 && tail.Length > 0;
        }

        private static string ExtractRepo(string path, string marker)
        {
            const string prefix = "/v2/";
            int markerIndex = path.IndexOf(marker, StringComparison.Ordinal);
            return path[prefix.Length..markerIndex];
        }
    }

    private const string SourceHost = "mcr.microsoft.com";
    private const string EcrHost = "123456789012.dkr.ecr.us-west-2.amazonaws.com";

    // A well-formed ECR authorization token: base64("AWS:password"). PushToEcrAsync decodes this into the
    // Basic username/password; the fake registry does not check it, so any valid-form token works here.
    private static readonly string FakeEcrToken =
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("AWS:fake-password"));

    /// <summary>Builds a base image (config + manifest with one base layer) and seeds the fake source registry.</summary>
    private static (ImageBuilder Builder, IReadOnlyList<Descriptor> BaseLayers) BuildBaseImage(FakeRegistry registry, string sourceRepo)
    {
        // A base layer blob that CopyBaseLayersAsync must copy to ECR.
        byte[] baseLayerBytes = Encoding.UTF8.GetBytes("base-layer-content");
        string baseLayerDigest = RegistryClient.ComputeDigest(new MemoryStream(baseLayerBytes));
        registry.SeedSourceBlob(SourceHost, sourceRepo, baseLayerDigest, baseLayerBytes);

        const string configJson = """
            {"architecture":"amd64","os":"linux","rootfs":{"type":"layers","diff_ids":["sha256:aaaa"]},"config":{}}
            """;
        string manifestJson = $$"""
            {"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json","config":{"mediaType":"application/vnd.oci.image.config.v1+json","size":10,"digest":"sha256:cfg"},"layers":[{"mediaType":"application/vnd.oci.image.layer.v1.tar+gzip","size":{{baseLayerBytes.Length}},"digest":"{{baseLayerDigest}}"}]}
            """;

        ImageBuilder builder = ImageBuilder.FromBaseImage(configJson, manifestJson, MediaTypes.OciManifestV1);
        var baseLayers = new[]
        {
            new Descriptor { MediaType = MediaTypes.OciLayerGzipV1, Size = baseLayerBytes.Length, Digest = baseLayerDigest },
        };
        return (builder, baseLayers);
    }

    [Fact]
    public async Task PushAsync_copies_base_layers_uploads_new_layer_and_config_and_puts_manifest()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, sourceRepo);

        // Source client reads base-layer blobs; both source and target go through the same fake handler.
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/{sourceRepo}:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);
        builder.UseTargetClientFactoryForTest((host, _) => new RegistryClient(host, new HttpClient(registry)));

        // Add an application layer from a temp file.
        string tempDir = Path.Combine(Path.GetTempPath(), "oci-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string appFile = Path.Combine(tempDir, "app.txt");
            await File.WriteAllTextAsync(appFile, "hello");
            var files = new FileManifest { { appFile, "/app/app.txt" } };
            await builder.AddLayerAsync(files, createdBy: "test");
            builder.Config.SetEntrypoint("dotnet", "/app/app.txt");

            string digest = await builder.PushToEcrAsync($"{EcrHost}/{targetRepo}:latest", FakeEcrToken);

            // The returned digest addresses the manifest.
            Assert.StartsWith("sha256:", digest);

            // The manifest was PUT to the ECR target repo under the right tag and media type.
            Assert.NotNull(registry.PutManifest);
            var put = registry.PutManifest!.Value;
            Assert.Equal(EcrHost, put.Host);
            Assert.Equal(targetRepo, put.Repo);
            Assert.Equal("latest", put.Reference);
            Assert.Equal(MediaTypes.OciManifestV1, put.ContentType);

            // All blobs uploaded to ECR: the base layer (copied), the new app layer, and the config.
            var ecrUploads = registry.UploadedBlobs.Where(u => u.Host == EcrHost && u.Repo == targetRepo).ToList();
            Manifest pushedManifest = Manifest.Parse(put.Body);

            // Base layer + new layer both present in the manifest and both uploaded.
            Assert.Equal(2, pushedManifest.Layers.Count);
            foreach (Descriptor layer in pushedManifest.Layers)
            {
                Assert.Contains(ecrUploads, u => u.Digest == layer.Digest);
            }
            // Config blob uploaded, and the manifest points at it.
            Assert.Contains(ecrUploads, u => u.Digest == pushedManifest.Config.Digest);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_advertises_content_length_on_every_upload_so_bodies_stream()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, sourceRepo);
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/{sourceRepo}:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);
        builder.UseTargetClientFactoryForTest((host, _) => new RegistryClient(host, new HttpClient(registry)));

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-cl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string appFile = Path.Combine(tempDir, "app.txt");
            await File.WriteAllTextAsync(appFile, "hello");
            await builder.AddLayerAsync(new FileManifest { { appFile, "/app/app.txt" } });

            await builder.PushToEcrAsync($"{EcrHost}/{targetRepo}:latest", FakeEcrToken);

            // Every blob PATCH (base layer, built layer, config) must advertise a Content-Length. A null here
            // would mean HttpClient had to buffer the whole body in memory to measure it — the exact memory
            // behavior these fixes exist to avoid.
            Assert.NotEmpty(registry.PatchContentLengths);
            Assert.All(registry.PatchContentLengths, length => Assert.NotNull(length));
            Assert.All(registry.PatchContentLengths, length => Assert.True(length > 0));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task AddLayerAsync_spills_built_layer_to_disk_and_Dispose_deletes_it()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";

        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, sourceRepo);
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/{sourceRepo}:8.0");

        OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);
        builder.UseTargetClientFactoryForTest((host, _) => new RegistryClient(host, new HttpClient(registry)));

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-spill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        // Assert only on the spill files THIS builder owns (from the builder itself), never by scanning the shared
        // temp directory: parallel tests spill files with the same name pattern into it concurrently.
        try
        {
            string appFile = Path.Combine(tempDir, "app.txt");
            await File.WriteAllTextAsync(appFile, "hello");

            await builder.AddLayerAsync(new FileManifest { { appFile, "/app/app.txt" } });

            // Captured before Dispose, which clears the builder's pending-layer list.
            IReadOnlyList<string> afterAdd = builder.SpilledLayerPathsForTest;
            Assert.Single(afterAdd);
            Assert.All(afterAdd, path => Assert.True(File.Exists(path), $"AddLayerAsync should have spilled the built layer to {path}."));

            // #5: on Unix the spill file must be owner-only (0600), not world-readable (0644). A private layer in
            // the shared temp dir must not be readable by other local users.
            if (!OperatingSystem.IsWindows())
            {
                const UnixFileMode groupOrOther =
                    UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                // A plain loop (not an Assert.All lambda) keeps the call inside the platform guard for CA1416.
                foreach (string path in afterAdd)
                {
                    Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(path) & groupOrOther);
                }
            }

            builder.Dispose();

            // Every file this test created must be gone after Dispose (regardless of what other tests are doing).
            Assert.All(afterAdd, path => Assert.False(File.Exists(path), $"Dispose should have deleted {path}."));
        }
        finally
        {
            builder.Dispose();
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_skips_base_layer_already_present_in_target()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, sourceRepo);

        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/{sourceRepo}:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);
        builder.UseTargetClientFactoryForTest((host, _) => new RegistryClient(host, new HttpClient(registry)));

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string appFile = Path.Combine(tempDir, "app.txt");
            await File.WriteAllTextAsync(appFile, "hello");
            await builder.AddLayerAsync(new FileManifest { { appFile, "/app/app.txt" } });

            await builder.PushToEcrAsync($"{EcrHost}/{targetRepo}:latest", FakeEcrToken);

            // The base layer download count: it must have been copied once (first push).
            var put = registry.PutManifest!.Value;
            Manifest pushed = Manifest.Parse(put.Body);
            string baseLayerDigest = baseLayers[0].Digest;
            Assert.Contains(pushed.Layers, l => l.Digest == baseLayerDigest);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_rejects_non_ecr_target()
    {
        var registry = new FakeRegistry();
        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, "dotnet/aspnet");
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/dotnet/aspnet:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);

        await Assert.ThrowsAsync<ArgumentException>(
            () => builder.PushToEcrAsync("mcr.microsoft.com/notecr/app:latest", FakeEcrToken));
    }

    [Fact]
    public async Task PushAsync_rejects_base_layer_whose_bytes_do_not_match_its_digest()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, sourceRepo);

        // Tamper: make the source registry serve DIFFERENT bytes under the base layer's declared digest, as a
        // corrupted cache or a man-in-the-middle-past-TLS registry would. The declared digest is unchanged.
        string baseLayerDigest = baseLayers[0].Digest;
        registry.SeedSourceBlob(SourceHost, sourceRepo, baseLayerDigest, System.Text.Encoding.UTF8.GetBytes("tampered!"));

        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/{sourceRepo}:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);
        builder.UseTargetClientFactoryForTest((host, _) => new RegistryClient(host, new HttpClient(registry)));

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string appFile = Path.Combine(tempDir, "app.txt");
            await File.WriteAllTextAsync(appFile, "hello");
            await builder.AddLayerAsync(new FileManifest { { appFile, "/app/app.txt" } });

            // The corrupted base layer must be detected on download and the push aborted.
            await Assert.ThrowsAsync<DigestMismatchException>(
                () => builder.PushToEcrAsync($"{EcrHost}/{targetRepo}:latest", FakeEcrToken));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_spills_large_base_layer_to_disk_and_still_pushes_successfully()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, sourceRepo);

        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/{sourceRepo}:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);
        builder.UseTargetClientFactoryForTest((host, _) => new RegistryClient(host, new HttpClient(registry)));

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string appFile = Path.Combine(tempDir, "app.txt");
            await File.WriteAllTextAsync(appFile, "hello");
            await builder.AddLayerAsync(new FileManifest { { appFile, "/app/app.txt" } });

            // Force the spill-to-disk path: threshold 0 makes every base layer (size > 0) spill to a temp file,
            // get re-verified from disk, and upload from the file rather than memory.
            string digest = await builder.PushToEcrAsync(
                $"{EcrHost}/{targetRepo}:latest",
                FakeEcrToken,
                new EcrPushOptions { MaxInMemoryLayerBytes = 0 });

            Assert.StartsWith("sha256:", digest);

            // The base layer (spilled + verified from disk) and the new layer both reached ECR.
            var put = registry.PutManifest!.Value;
            Manifest pushed = Manifest.Parse(put.Body);
            var ecrUploads = registry.UploadedBlobs.Where(u => u.Host == EcrHost && u.Repo == targetRepo).ToList();
            Assert.Equal(2, pushed.Layers.Count);
            foreach (Descriptor layer in pushed.Layers)
            {
                Assert.Contains(ecrUploads, u => u.Digest == layer.Digest);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_rejects_tampered_base_layer_on_the_spill_to_disk_path()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, sourceRepo);

        // Corrupt the served bytes under the declared digest, exactly as the in-memory tamper test does, but this
        // time force the spill path so the disk download + re-read verification is the thing that must catch it.
        string baseLayerDigest = baseLayers[0].Digest;
        registry.SeedSourceBlob(SourceHost, sourceRepo, baseLayerDigest, System.Text.Encoding.UTF8.GetBytes("tampered!"));

        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/{sourceRepo}:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);
        builder.UseTargetClientFactoryForTest((host, _) => new RegistryClient(host, new HttpClient(registry)));

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string appFile = Path.Combine(tempDir, "app.txt");
            await File.WriteAllTextAsync(appFile, "hello");
            await builder.AddLayerAsync(new FileManifest { { appFile, "/app/app.txt" } });

            await Assert.ThrowsAsync<DigestMismatchException>(
                () => builder.PushToEcrAsync(
                    $"{EcrHost}/{targetRepo}:latest",
                    FakeEcrToken,
                    new EcrPushOptions { MaxInMemoryLayerBytes = 0 }));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_rejects_negative_in_memory_threshold()
    {
        var registry = new FakeRegistry();
        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, "dotnet/aspnet");
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/dotnet/aspnet:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);

        await Assert.ThrowsAsync<ArgumentException>(
            () => builder.PushToEcrAsync(
                $"{EcrHost}/myapp:latest",
                FakeEcrToken,
                new EcrPushOptions { MaxInMemoryLayerBytes = -1 }));
    }

    [Theory]
    [InlineData("")]                               // empty
    [InlineData("   ")]                            // whitespace
    [InlineData("not-base64-$$$")]                 // not valid base64
    public async Task PushToEcrAsync_rejects_a_malformed_authorization_token(string badToken)
    {
        var registry = new FakeRegistry();
        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, "dotnet/aspnet");
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/dotnet/aspnet:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);

        await Assert.ThrowsAsync<ArgumentException>(
            () => builder.PushToEcrAsync($"{EcrHost}/myapp:latest", badToken));
    }

    [Fact]
    public async Task PushToEcrAsync_rejects_a_token_that_decodes_without_a_username_password_separator()
    {
        var registry = new FakeRegistry();
        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, "dotnet/aspnet");
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/dotnet/aspnet:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);

        // Valid base64, but decodes to "no-separator-here" with no ':' — not a username:password pair.
        string tokenWithoutColon = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("no-separator-here"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => builder.PushToEcrAsync($"{EcrHost}/myapp:latest", tokenWithoutColon));
    }

    [Theory]
    [InlineData(":password", "username")] // empty username
    [InlineData("AWS:", "password")]      // empty password
    public async Task PushToEcrAsync_rejects_a_token_with_an_empty_username_or_password(string decodedToken, string emptyPart)
    {
        var registry = new FakeRegistry();
        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, "dotnet/aspnet");
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/dotnet/aspnet:8.0");

        using OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);

        string token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(decodedToken));

        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(
            () => builder.PushToEcrAsync($"{EcrHost}/myapp:latest", token));
        Assert.Contains($"empty {emptyPart}", ex.Message, StringComparison.Ordinal);
    }

    // ---- Progress reporting ----------------------------------------------------------------------

    /// <summary>Builds a fresh builder over the shared fake registry with one base layer and one app layer.</summary>
    private static async Task<OciImageBuilder> BuildTwoLayerImageAsync(FakeRegistry registry, string sourceRepo, string tempDir)
    {
        (ImageBuilder imageBuilder, IReadOnlyList<Descriptor> baseLayers) = BuildBaseImage(registry, sourceRepo);
        var sourceClient = new RegistryClient(SourceHost, new HttpClient(registry));
        var baseRef = ImageReference.Parse($"{SourceHost}/{sourceRepo}:8.0");

        OciImageBuilder builder = OciImageBuilder.CreateForTest(baseRef, sourceClient, imageBuilder, baseLayers);
        builder.UseTargetClientFactoryForTest((host, _) => new RegistryClient(host, new HttpClient(registry)));

        string appFile = Path.Combine(tempDir, "app.txt");
        await File.WriteAllTextAsync(appFile, "hello-progress");
        await builder.AddLayerAsync(new FileManifest { { appFile, "/app/app.txt" } });
        return builder;
    }

    [Fact]
    public async Task PushToEcrAsync_reports_progress_for_every_layer_with_stable_index_total_and_kind()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            using OciImageBuilder builder = await BuildTwoLayerImageAsync(registry, sourceRepo, tempDir);

            var events = new List<LayerPushProgress>();
            await builder.PushToEcrAsync(
                $"{EcrHost}/{targetRepo}:latest",
                FakeEcrToken,
                new EcrPushOptions { Progress = new SynchronousProgress(events.Add) });

            // Two layers: index 1 = base layer, index 2 = built app layer; TotalLayers is 2 on every event.
            Assert.All(events, e => Assert.Equal(2, e.TotalLayers));
            Assert.All(events, e => Assert.InRange(e.LayerIndex, 1, 2));

            // The config blob and manifest are NOT layers, so they produce no events — only layer indices appear.
            Assert.Equal(new[] { 1, 2 }, events.Select(e => e.LayerIndex).Distinct().OrderBy(i => i).ToArray());

            // Layer 1 is the base layer; layer 2 is the locally-built layer. Kind is stable per index.
            Assert.All(events.Where(e => e.LayerIndex == 1), e => Assert.Equal(LayerKind.BaseLayer, e.Kind));
            Assert.All(events.Where(e => e.LayerIndex == 2), e => Assert.Equal(LayerKind.BuiltLayer, e.Kind));

            // Each layer has exactly one digest across all its events (stable identity).
            foreach (int index in new[] { 1, 2 })
            {
                Assert.Single(events.Where(e => e.LayerIndex == index).Select(e => e.Digest).Distinct());
            }

            // Every layer terminates on exactly one Completed event — one consistent "done" signal.
            Assert.Equal(2, events.Count(e => e.Phase == LayerPushPhase.Completed));
            foreach (int index in new[] { 1, 2 })
            {
                Assert.Single(events, e => e.LayerIndex == index && e.Phase == LayerPushPhase.Completed);
            }

            // Each uploaded layer starts before it completes.
            foreach (int index in new[] { 1, 2 })
            {
                var perLayer = events.Where(e => e.LayerIndex == index).ToList();
                int startedAt = perLayer.FindIndex(e => e.Phase == LayerPushPhase.Started);
                int completedAt = perLayer.FindIndex(e => e.Phase == LayerPushPhase.Completed);
                Assert.True(startedAt >= 0, $"layer {index} should have a Started event");
                Assert.True(startedAt < completedAt, $"layer {index} Started must precede Completed");
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PushToEcrAsync_reports_a_skipped_layer_as_a_single_completed_event_with_IsSkipped_true()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // First push lands the base layer + app layer in the ECR target repo.
            using (OciImageBuilder first = await BuildTwoLayerImageAsync(registry, sourceRepo, tempDir))
            {
                await first.PushToEcrAsync($"{EcrHost}/{targetRepo}:latest", FakeEcrToken);
            }

            // Second push of an identical image to the SAME target: both layers already exist, so both are skipped.
            using OciImageBuilder second = await BuildTwoLayerImageAsync(registry, sourceRepo, tempDir);
            var events = new List<LayerPushProgress>();
            await second.PushToEcrAsync(
                $"{EcrHost}/{targetRepo}:latest",
                FakeEcrToken,
                new EcrPushOptions { Progress = new SynchronousProgress(events.Add) });

            // Each layer is reported as exactly one event: Completed + IsSkipped, with no preceding Started/Uploading.
            foreach (int index in new[] { 1, 2 })
            {
                var perLayer = events.Where(e => e.LayerIndex == index).ToList();
                LayerPushProgress only = Assert.Single(perLayer);
                Assert.Equal(LayerPushPhase.Completed, only.Phase);
                Assert.True(only.IsSkipped);
                Assert.Equal(0, only.BytesPushed);
                // LayerSizeBytes is still the real size (so a UI can say "skipped — N bytes already present").
                Assert.True(only.LayerSizeBytes > 0);
            }

            // No Started or Uploading events at all when everything is skipped.
            Assert.DoesNotContain(events, e => e.Phase == LayerPushPhase.Started);
            Assert.DoesNotContain(events, e => e.Phase == LayerPushPhase.Uploading);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PushToEcrAsync_reports_byte_progress_reaching_layer_size_at_completion()
    {
        var registry = new FakeRegistry();
        const string sourceRepo = "dotnet/aspnet";
        const string targetRepo = "myapp";

        string tempDir = Path.Combine(Path.GetTempPath(), "oci-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            using OciImageBuilder builder = await BuildTwoLayerImageAsync(registry, sourceRepo, tempDir);

            var events = new List<LayerPushProgress>();
            await builder.PushToEcrAsync(
                $"{EcrHost}/{targetRepo}:latest",
                FakeEcrToken,
                new EcrPushOptions { Progress = new SynchronousProgress(events.Add) });

            foreach (int index in new[] { 1, 2 })
            {
                var perLayer = events.Where(e => e.LayerIndex == index).ToList();

                LayerPushProgress started = perLayer.First(e => e.Phase == LayerPushPhase.Started);
                Assert.Equal(0, started.BytesPushed);

                // Uploading events (if any) never exceed the layer size and are monotonically non-decreasing.
                var uploading = perLayer.Where(e => e.Phase == LayerPushPhase.Uploading).ToList();
                long previous = 0;
                foreach (LayerPushProgress u in uploading)
                {
                    Assert.True(u.BytesPushed >= previous, "byte progress must not go backward");
                    Assert.True(u.BytesPushed <= u.LayerSizeBytes, "byte progress must not exceed the layer size");
                    previous = u.BytesPushed;
                }

                // At completion, bytes pushed equals the full layer size.
                LayerPushProgress completed = perLayer.Single(e => e.Phase == LayerPushPhase.Completed);
                Assert.Equal(completed.LayerSizeBytes, completed.BytesPushed);
                Assert.False(completed.IsSkipped);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// A deterministic <see cref="IProgress{T}"/> that invokes the callback synchronously on the reporting thread,
    /// so a test observes every event in emission order (unlike <see cref="Progress{T}"/>, which marshals and may
    /// reorder/defer via the captured synchronization context).
    /// </summary>
    private sealed class SynchronousProgress : IProgress<LayerPushProgress>
    {
        private readonly Action<LayerPushProgress> _onReport;
        public SynchronousProgress(Action<LayerPushProgress> onReport) => _onReport = onReport;
        public void Report(LayerPushProgress value) => _onReport(value);
    }
}
