using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.OCI.Builder;
using Xunit;
using Xunit.Abstractions;

namespace AWS.OCI.Builder.IntegrationTests;

/// <summary>
/// End-to-end integration test that pushes a real image to Amazon ECR using the library's public API, then
/// verifies the image landed by querying ECR. AWS credentials and region are taken from the environment (the AWS SDK default chain); set
/// <c>AWS_SHARED_CREDENTIALS_FILE</c> / <c>AWS_PROFILE</c> / <c>AWS_REGION</c> as needed before running.
/// </summary>
/// <remarks>
/// <para>What it does, end to end:</para>
/// <list type="number">
///   <item>Ensures a dedicated ECR repository exists (creates it if missing).</item>
///   <item>Pulls a small public base image, overlays a generated file as a new layer, and pushes to the repo
///     under a unique tag via <see cref="OciImageBuilder"/>.</item>
///   <item>Calls ECR <c>DescribeImages</c> to confirm the pushed tag exists and its digest matches what
///     <see cref="OciImageBuilder.PushToEcrAsync"/> returned.</item>
///   <item>Cleans up by deleting just the tag it pushed (the repository is left in place).</item>
/// </list>
/// <para>
/// Overridable via environment variables: <c>OCI_ECR_REPOSITORY</c> (default <c>aws-oci-builder-itest</c>),
/// <c>OCI_ECR_REGION</c> (default <c>us-west-2</c>), <c>OCI_ECR_BASE_IMAGE</c>
/// (default <c>mcr.microsoft.com/dotnet/runtime-deps:8.0</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public class EcrPushIntegrationTests
{
    private const string DefaultRepository = "aws-oci-builder-itest";
    private const string DefaultRegion = "us-west-2";
    private const string DefaultBaseImage = "mcr.microsoft.com/dotnet/runtime-deps:8.0";
    private const string Platform = "linux/amd64";

    private readonly ITestOutputHelper _output;

    public EcrPushIntegrationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Pushes_image_to_real_ecr_and_verifies_it()
    {
        string repository = EnvOr("OCI_ECR_REPOSITORY", DefaultRepository);
        string regionName = EnvOr("OCI_ECR_REGION", DefaultRegion);
        string baseImage = EnvOr("OCI_ECR_BASE_IMAGE", DefaultBaseImage);
        RegionEndpoint region = RegionEndpoint.GetBySystemName(regionName);
        string tag = "itest-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];

        using var ecr = new AmazonECRClient(region);

        // ---- 1. Resolve the registry host and ensure the repository exists. ----
        string accountId = await GetAccountIdAsync(ecr);
        string registryHost = $"{accountId}.dkr.ecr.{regionName}.amazonaws.com";
        string targetImage = $"{registryHost}/{repository}:{tag}";
        _output.WriteLine($"Target: {targetImage}");

        await EnsureRepositoryAsync(ecr, repository);

        string? pushedDigest = null;
        try
        {
            // ---- 2. Build from the base image + a generated layer, and push. ----
            string payloadFile = Path.Combine(Path.GetTempPath(), $"oci-itest-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(payloadFile, "Hello from the AWS.OCI.Builder integration test.\n");

            try
            {
                using OciImageBuilder builder = await OciImageBuilder.FromBaseImageAsync(baseImage, Platform);
                await builder.AddLayerAsync(
                    new FileManifest { { payloadFile, "/app/hello.txt" } },
                    createdBy: "AWS.OCI.Builder integration test");
                builder.Config.SetWorkingDirectory("/app");
                builder.Config.SetLabel("com.amazonaws.oci-builder.itest", tag);

                // Region is inferred from the ECR host in the target; the auth token is fetched via the SDK.
                string authToken = await GetAuthorizationTokenStringAsync(ecr);

                // Collect push progress so we can assert the event contract against real ECR byte streaming.
                var events = new List<LayerPushProgress>();
                var options = new EcrPushOptions
                {
                    Progress = new Progress<LayerPushProgress>(e =>
                    {
                        lock (events) { events.Add(e); }
                    }),
                };
                pushedDigest = await builder.PushToEcrAsync(targetImage, authToken, options);

                // Verify the progress contract end to end. (Progress<T> marshals callbacks, so snapshot under lock.)
                List<LayerPushProgress> seen;
                lock (events) { seen = events.ToList(); }

                Assert.NotEmpty(seen);
                int totalLayers = seen[0].TotalLayers;
                Assert.All(seen, e => Assert.Equal(totalLayers, e.TotalLayers));
                Assert.All(seen, e => Assert.InRange(e.LayerIndex, 1, totalLayers));

                // Every layer (1..totalLayers) terminates on exactly one Completed event.
                for (int index = 1; index <= totalLayers; index++)
                {
                    LayerPushProgress completed = Assert.Single(
                        seen, e => e.LayerIndex == index && e.Phase == LayerPushPhase.Completed);
                    // A non-skipped layer's byte count reaches its size; a skipped one reports 0 of a known size.
                    if (completed.IsSkipped)
                    {
                        Assert.Equal(0, completed.BytesPushed);
                    }
                    else
                    {
                        Assert.Equal(completed.LayerSizeBytes, completed.BytesPushed);
                    }
                }
                _output.WriteLine($"Progress: {seen.Count} events across {totalLayers} layers.");
            }
            finally
            {
                File.Delete(payloadFile);
            }

            _output.WriteLine($"Pushed digest: {pushedDigest}");
            Assert.StartsWith("sha256:", pushedDigest);

            // ---- 3. Verify via ECR that the tag exists and the digest matches. ----
            DescribeImagesResponse described = await ecr.DescribeImagesAsync(new DescribeImagesRequest
            {
                RepositoryName = repository,
                ImageIds = new List<ImageIdentifier> { new() { ImageTag = tag } },
            });

            ImageDetail detail = Assert.Single(described.ImageDetails);
            Assert.Contains(tag, detail.ImageTags);
            Assert.Equal(pushedDigest, detail.ImageDigest);
            _output.WriteLine($"Verified image {detail.ImageDigest} ({detail.ImageSizeInBytes} bytes) in ECR.");
        }
        finally
        {
            // ---- 4. Best-effort cleanup: delete only the tag this test pushed. ----
            try
            {
                await ecr.BatchDeleteImageAsync(new BatchDeleteImageRequest
                {
                    RepositoryName = repository,
                    ImageIds = new List<ImageIdentifier> { new() { ImageTag = tag } },
                });
                _output.WriteLine($"Cleaned up tag {tag}.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Cleanup of tag {tag} failed (non-fatal): {ex.Message}");
            }
        }
    }

    [Fact]
    public async Task Pushes_image_to_real_ecr_using_the_spill_to_disk_path_for_large_base_layers()
    {
        string repository = EnvOr("OCI_ECR_REPOSITORY", DefaultRepository);
        string regionName = EnvOr("OCI_ECR_REGION", DefaultRegion);
        string baseImage = EnvOr("OCI_ECR_BASE_IMAGE", DefaultBaseImage);
        RegionEndpoint region = RegionEndpoint.GetBySystemName(regionName);
        string tag = "itest-spill-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];

        using var ecr = new AmazonECRClient(region);
        string accountId = await GetAccountIdAsync(ecr);
        string registryHost = $"{accountId}.dkr.ecr.{regionName}.amazonaws.com";
        string targetImage = $"{registryHost}/{repository}:{tag}";
        _output.WriteLine($"Target: {targetImage}");

        await EnsureRepositoryAsync(ecr, repository);

        string? pushedDigest = null;
        try
        {
            string payloadFile = Path.Combine(Path.GetTempPath(), $"oci-itest-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(payloadFile, "Hello from the AWS.OCI.Builder spill-to-disk integration test.\n");

            try
            {
                using OciImageBuilder builder = await OciImageBuilder.FromBaseImageAsync(baseImage, Platform);
                await builder.AddLayerAsync(
                    new FileManifest { { payloadFile, "/app/hello.txt" } },
                    createdBy: "AWS.OCI.Builder spill-to-disk integration test");
                builder.Config.SetWorkingDirectory("/app");

                // Force the spill-to-disk copy path: a 1 MiB threshold is well below the base image's real layer
                // sizes (its largest runtime-deps layer is tens of MiB), so those layers are streamed through a
                // temp file, re-verified from disk, and uploaded from the file rather than buffered in memory.
                var options = new EcrPushOptions { MaxInMemoryLayerBytes = 1L * 1024 * 1024 };
                string authToken = await GetAuthorizationTokenStringAsync(ecr);
                pushedDigest = await builder.PushToEcrAsync(targetImage, authToken, options);
            }
            finally
            {
                File.Delete(payloadFile);
            }

            _output.WriteLine($"Pushed digest (spill path): {pushedDigest}");
            Assert.StartsWith("sha256:", pushedDigest);

            // Verify via ECR that the tag exists and the digest matches — proving the disk-spilled layers produced
            // a valid, consistent image in ECR (every blob committed its digest check).
            DescribeImagesResponse described = await ecr.DescribeImagesAsync(new DescribeImagesRequest
            {
                RepositoryName = repository,
                ImageIds = new List<ImageIdentifier> { new() { ImageTag = tag } },
            });

            ImageDetail detail = Assert.Single(described.ImageDetails);
            Assert.Contains(tag, detail.ImageTags);
            Assert.Equal(pushedDigest, detail.ImageDigest);
            _output.WriteLine($"Verified spill-path image {detail.ImageDigest} ({detail.ImageSizeInBytes} bytes) in ECR.");
        }
        finally
        {
            try
            {
                await ecr.BatchDeleteImageAsync(new BatchDeleteImageRequest
                {
                    RepositoryName = repository,
                    ImageIds = new List<ImageIdentifier> { new() { ImageTag = tag } },
                });
                _output.WriteLine($"Cleaned up tag {tag}.");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Cleanup of tag {tag} failed (non-fatal): {ex.Message}");
            }
        }
    }

    [Fact]
    public async Task Pushed_executable_file_keeps_its_0755_mode_in_ecr()
    {
        string repository = EnvOr("OCI_ECR_REPOSITORY", DefaultRepository);
        string regionName = EnvOr("OCI_ECR_REGION", DefaultRegion);
        string baseImage = EnvOr("OCI_ECR_BASE_IMAGE", DefaultBaseImage);
        RegionEndpoint region = RegionEndpoint.GetBySystemName(regionName);
        string tag = "itest-exec-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];

        using var ecr = new AmazonECRClient(region);
        string accountId = await GetAccountIdAsync(ecr);
        string registryHost = $"{accountId}.dkr.ecr.{regionName}.amazonaws.com";
        string targetImage = $"{registryHost}/{repository}:{tag}";
        await EnsureRepositoryAsync(ecr, repository);

        string? newLayerDigest = null;
        try
        {
            string scriptFile = Path.Combine(Path.GetTempPath(), $"oci-itest-{Guid.NewGuid():N}.sh");
            await File.WriteAllTextAsync(scriptFile, "#!/bin/sh\necho hi\n");
            try
            {
                using OciImageBuilder builder = await OciImageBuilder.FromBaseImageAsync(baseImage, Platform);
                // Mark the script executable regardless of host — this is the behavior that must survive to ECR,
                // and on Windows there would be no source executable bit to rely on.
                var files = new FileManifest();
                files.AddExecutable(scriptFile, "/app/run.sh");
                await builder.AddLayerAsync(files, createdBy: "exec-bit itest");
                string authToken = await GetAuthorizationTokenStringAsync(ecr);
                await builder.PushToEcrAsync(targetImage, authToken);
            }
            finally
            {
                File.Delete(scriptFile);
            }

            // Pull the pushed manifest, find the LAST layer (the one we added), download its blob, and inspect the
            // mode of our file entry inside the gzipped tar.
            var auth = await ecr.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
            string basic = auth.AuthorizationData[0].AuthorizationToken;

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
            foreach (string accept in new[]
            {
                "application/vnd.oci.image.manifest.v1+json",
                "application/vnd.docker.distribution.manifest.v2+json",
            })
            {
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(accept));
            }

            string manifestJson = await http.GetStringAsync($"https://{registryHost}/v2/{repository}/manifests/{tag}");
            using var manifestDoc = System.Text.Json.JsonDocument.Parse(manifestJson);
            var layers = manifestDoc.RootElement.GetProperty("layers");
            newLayerDigest = layers[layers.GetArrayLength() - 1].GetProperty("digest").GetString();
            _output.WriteLine($"New layer blob: {newLayerDigest}");

            byte[] gzipped = await http.GetByteArrayAsync($"https://{registryHost}/v2/{repository}/blobs/{newLayerDigest}");
            UnixFileMode mode = ReadEntryMode(gzipped, "app/run.sh");
            _output.WriteLine($"app/run.sh mode in ECR: {mode}");

            Assert.True(mode.HasFlag(UnixFileMode.UserExecute), $"expected the owner-execute bit to survive, got {mode}");
        }
        finally
        {
            try
            {
                await ecr.BatchDeleteImageAsync(new BatchDeleteImageRequest
                {
                    RepositoryName = repository,
                    ImageIds = new List<ImageIdentifier> { new() { ImageTag = tag } },
                });
            }
            catch (Exception ex) { _output.WriteLine($"Cleanup failed (non-fatal): {ex.Message}"); }
        }
    }

    /// <summary>Decompresses a gzipped tar blob and returns the Unix mode of the entry named <paramref name="entryName"/>.</summary>
    private static UnixFileMode ReadEntryMode(byte[] gzippedTar, string entryName)
    {
        using var input = new MemoryStream(gzippedTar);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new System.Formats.Tar.TarReader(gzip);
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.Name.TrimStart('\0') == entryName)
            {
                return entry.Mode;
            }
        }
        throw new Xunit.Sdk.XunitException($"Entry '{entryName}' not found in the pulled layer.");
    }

    private static async Task<string> GetAccountIdAsync(IAmazonECR ecr)
    {
        // GetAuthorizationToken's response identifies the registry (account) the caller is authenticated against.
        GetAuthorizationTokenResponse token = await ecr.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
        AuthorizationData data = Assert.Single(token.AuthorizationData);

        // proxyEndpoint is https://<account>.dkr.ecr.<region>.amazonaws.com — extract the account id from the host.
        var proxy = new Uri(data.ProxyEndpoint);
        return proxy.Host.Split('.')[0];
    }

    /// <summary>Fetches the ECR authorization token string (base64 "AWS:password") to pass to PushToEcrAsync.</summary>
    private static async Task<string> GetAuthorizationTokenStringAsync(IAmazonECR ecr)
    {
        GetAuthorizationTokenResponse token = await ecr.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
        AuthorizationData data = Assert.Single(token.AuthorizationData);
        return data.AuthorizationToken;
    }

    private async Task EnsureRepositoryAsync(IAmazonECR ecr, string repository)
    {
        try
        {
            await ecr.CreateRepositoryAsync(new CreateRepositoryRequest { RepositoryName = repository });
            _output.WriteLine($"Created repository {repository}.");
        }
        catch (RepositoryAlreadyExistsException)
        {
            _output.WriteLine($"Repository {repository} already exists.");
        }
    }

    private static string EnvOr(string name, string fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
