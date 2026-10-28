using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Amazon.OCI.Builder;
using Amazon.OCI.Builder.Images;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class ImageConfigTests
{
    private const string MinimalConfig = """
        {"architecture":"amd64","os":"linux","rootfs":{"type":"layers","diff_ids":["sha256:aaa"]},"history":[{"created":"2020-01-01T00:00:00Z"}],"config":{"Env":["PATH=/usr/bin"]}}
        """;

    [Fact]
    public void Parse_exposes_os_and_architecture()
    {
        ImageConfig config = ImageConfig.Parse(MinimalConfig);
        Assert.Equal("linux", config.Os);
        Assert.Equal("amd64", config.Architecture);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[1,2,3]")]   // not an object
    [InlineData("{ not json")]
    public void Parse_rejects_invalid_input(string json)
        => Assert.Throws<ArgumentException>(() => ImageConfig.Parse(json));

    [Fact]
    public void AddLayer_appends_diffId_and_history_entry()
    {
        ImageConfig config = ImageConfig.Parse(MinimalConfig);
        config.AddLayer("sha256:bbb", "ADD app layer");

        JsonObject root = (JsonObject)JsonNode.Parse(config.ToJsonString())!;
        JsonArray diffIds = (JsonArray)root["rootfs"]!["diff_ids"]!;
        JsonArray history = (JsonArray)root["history"]!;

        Assert.Equal(2, diffIds.Count);
        Assert.Equal("sha256:bbb", diffIds[1]!.GetValue<string>());
        Assert.Equal(2, history.Count);
        Assert.Equal("ADD app layer", history[1]!["created_by"]!.GetValue<string>());
    }

    [Fact]
    public void SetEnvironmentVariable_replaces_existing_key_in_place()
    {
        ImageConfig config = ImageConfig.Parse(MinimalConfig);
        config.SetEnvironmentVariable("PATH", "/custom/bin");       // overwrite existing
        config.SetEnvironmentVariable("ASPNETCORE_URLS", "http://+:8080"); // add new

        JsonObject root = (JsonObject)JsonNode.Parse(config.ToJsonString())!;
        JsonArray env = (JsonArray)root["config"]!["Env"]!;

        Assert.Equal(2, env.Count);
        Assert.Contains("PATH=/custom/bin", env.Select(e => e!.GetValue<string>()));
        Assert.DoesNotContain("PATH=/usr/bin", env.Select(e => e!.GetValue<string>()));
        Assert.Contains("ASPNETCORE_URLS=http://+:8080", env.Select(e => e!.GetValue<string>()));
    }

    [Fact]
    public void Sets_entrypoint_cmd_workdir_user_labels_and_ports()
    {
        ImageConfig config = ImageConfig.Parse(MinimalConfig);
        config.SetEntrypoint("dotnet", "/app/app.dll");
        config.SetCmd("--urls", "http://+:80");
        config.SetWorkingDirectory("/app");
        config.SetUser("1000");
        config.SetLabel("org.opencontainers.image.source", "https://example.com/repo");
        config.ExposePort("8080");

        JsonObject c = (JsonObject)JsonNode.Parse(config.ToJsonString())!["config"]!;

        Assert.Equal(new[] { "dotnet", "/app/app.dll" }, ((JsonArray)c["Entrypoint"]!).Select(e => e!.GetValue<string>()));
        Assert.Equal(new[] { "--urls", "http://+:80" }, ((JsonArray)c["Cmd"]!).Select(e => e!.GetValue<string>()));
        Assert.Equal("/app", c["WorkingDir"]!.GetValue<string>());
        Assert.Equal("1000", c["User"]!.GetValue<string>());
        Assert.Equal("https://example.com/repo", c["Labels"]!["org.opencontainers.image.source"]!.GetValue<string>());
        Assert.NotNull(c["ExposedPorts"]!["8080/tcp"]); // bare port got /tcp suffix
    }

    [Fact]
    public void Serialization_is_compact()
    {
        ImageConfig config = ImageConfig.Parse(MinimalConfig);
        string json = config.ToJsonString();
        Assert.DoesNotContain("\n", json);
        Assert.DoesNotContain("  ", json);
    }
}

public class ManifestTests
{
    [Fact]
    public void Round_trips_config_and_layers()
    {
        var manifest = new Manifest
        {
            MediaType = MediaTypes.OciManifestV1,
            Config = new Descriptor { MediaType = MediaTypes.OciImageConfigV1, Size = 10, Digest = "sha256:cfg" },
            Layers =
            {
                new Descriptor { MediaType = MediaTypes.OciLayerGzipV1, Size = 100, Digest = "sha256:l1" },
            },
        };

        Manifest parsed = Manifest.Parse(manifest.ToJsonString());

        Assert.Equal(2, parsed.SchemaVersion);
        Assert.Equal(MediaTypes.OciManifestV1, parsed.MediaType);
        Assert.Equal("sha256:cfg", parsed.Config.Digest);
        Assert.Single(parsed.Layers);
        Assert.Equal("sha256:l1", parsed.Layers[0].Digest);
    }

    [Fact]
    public void Property_order_is_schemaVersion_mediaType_config_layers()
    {
        var manifest = new Manifest
        {
            MediaType = MediaTypes.OciManifestV1,
            Config = new Descriptor { MediaType = MediaTypes.OciImageConfigV1, Size = 1, Digest = "sha256:cfg" },
        };
        string json = manifest.ToJsonString();

        int iSchema = json.IndexOf("schemaVersion", StringComparison.Ordinal);
        int iMedia = json.IndexOf("mediaType", StringComparison.Ordinal);
        int iConfig = json.IndexOf("\"config\"", StringComparison.Ordinal);
        int iLayers = json.IndexOf("\"layers\"", StringComparison.Ordinal);

        Assert.True(iSchema < iMedia && iMedia < iConfig && iConfig < iLayers);
    }
}

public class ImageIndexTests
{
    private const string IndexJson = """
        {"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[
          {"mediaType":"application/vnd.oci.image.manifest.v1+json","size":1,"digest":"sha256:amd","platform":{"os":"linux","architecture":"amd64"}},
          {"mediaType":"application/vnd.oci.image.manifest.v1+json","size":1,"digest":"sha256:arm","platform":{"os":"linux","architecture":"arm64"}},
          {"mediaType":"application/vnd.oci.image.manifest.v1+json","size":1,"digest":"sha256:att","platform":{"os":"unknown","architecture":"unknown"}}
        ]}
        """;

    [Fact]
    public void FindManifest_selects_matching_platform()
    {
        ImageIndex index = ImageIndex.Parse(IndexJson);
        PlatformManifestDescriptor? amd = index.FindManifest("linux", "amd64");
        PlatformManifestDescriptor? arm = index.FindManifest("linux", "arm64");

        Assert.Equal("sha256:amd", amd!.Digest);
        Assert.Equal("sha256:arm", arm!.Digest);
    }

    [Fact]
    public void FindManifest_returns_null_for_absent_platform()
    {
        ImageIndex index = ImageIndex.Parse(IndexJson);
        Assert.Null(index.FindManifest("windows", "amd64"));
    }
}

public class ImageBuilderTests
{
    private const string BaseConfig = """
        {"architecture":"amd64","os":"linux","rootfs":{"type":"layers","diff_ids":["sha256:base1"]},"history":[{"created":"2020-01-01T00:00:00Z"}]}
        """;

    private static string BaseManifest(string layerMediaType, string configMediaType) => $$"""
        {"schemaVersion":2,"mediaType":"{{MediaTypes.OciManifestV1}}","config":{"mediaType":"{{configMediaType}}","size":10,"digest":"sha256:basecfg"},"layers":[{"mediaType":"{{layerMediaType}}","size":100,"digest":"sha256:baselayer"}]}
        """;

    private static Layer FakeLayer(string mediaType)
    {
        // Build a real tiny layer so Digest/DiffId are genuine sha256 values. The layer is fully built (into a
        // throwaway stream) before returning, so its source directory can be removed immediately.
        string tmp = Path.Combine(Path.GetTempPath(), "aws-oci-ib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            string file = Path.Combine(tmp, "app.dll");
            File.WriteAllText(file, "payload");
            var manifest = new FileManifest { { file, "/app/app.dll" } };
            return Layer.CreateAsync(manifest, new MemoryStream(), mediaType).GetAwaiter().GetResult();
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    [Fact]
    public void AddLayer_keeps_manifest_layers_and_config_diffids_in_lockstep()
    {
        ImageBuilder builder = ImageBuilder.FromBaseImage(
            BaseConfig,
            BaseManifest(MediaTypes.OciLayerGzipV1, MediaTypes.OciImageConfigV1),
            MediaTypes.OciManifestV1);

        Layer layer = FakeLayer(MediaTypes.OciLayerGzipV1);
        builder.AddLayer(layer, "ADD app");

        BuiltImage built = builder.Build();
        Manifest manifest = Manifest.Parse(System.Text.Encoding.UTF8.GetString(built.ManifestBytes));
        JsonObject config = (JsonObject)System.Text.Json.Nodes.JsonNode.Parse(System.Text.Encoding.UTF8.GetString(built.ConfigBytes))!;

        // Base had 1 layer / 1 diff_id; after overlay both are 2 and the new entries match the layer.
        Assert.Equal(2, manifest.Layers.Count);
        Assert.Equal(layer.Digest, manifest.Layers[1].Digest);

        JsonArray diffIds = (JsonArray)config["rootfs"]!["diff_ids"]!;
        Assert.Equal(2, diffIds.Count);
        Assert.Equal(layer.DiffId, diffIds[1]!.GetValue<string>());
    }

    [Fact]
    public void Build_points_manifest_config_descriptor_at_actual_config_bytes()
    {
        ImageBuilder builder = ImageBuilder.FromBaseImage(
            BaseConfig,
            BaseManifest(MediaTypes.OciLayerGzipV1, MediaTypes.OciImageConfigV1),
            MediaTypes.OciManifestV1);
        builder.AddLayer(FakeLayer(MediaTypes.OciLayerGzipV1));

        BuiltImage built = builder.Build();
        Manifest manifest = Manifest.Parse(System.Text.Encoding.UTF8.GetString(built.ManifestBytes));

        // The manifest's config descriptor must match the real config bytes (size + digest), or registries reject it.
        string expectedDigest = "sha256:" + Convert.ToHexString(SHA256.HashData(built.ConfigBytes)).ToLowerInvariant();
        Assert.Equal(expectedDigest, manifest.Config.Digest);
        Assert.Equal(built.ConfigBytes.LongLength, manifest.Config.Size);
        Assert.Equal(built.ConfigDigest, manifest.Config.Digest);
    }

    [Fact]
    public void Detects_oci_schema_and_uses_oci_media_types()
    {
        ImageBuilder builder = ImageBuilder.FromBaseImage(
            BaseConfig,
            BaseManifest(MediaTypes.OciLayerGzipV1, MediaTypes.OciImageConfigV1),
            MediaTypes.OciManifestV1);

        Assert.Equal(MediaTypes.OciLayerGzipV1, builder.LayerMediaType);
        Assert.Equal(MediaTypes.OciImageConfigV1, builder.ConfigMediaType);
        Assert.Equal(MediaTypes.OciManifestV1, builder.ManifestMediaType);
    }

    [Fact]
    public void Detects_docker_schema_from_reported_media_type()
    {
        ImageBuilder builder = ImageBuilder.FromBaseImage(
            BaseConfig,
            BaseManifest(MediaTypes.DockerLayerGzip, MediaTypes.DockerImageConfigV1),
            MediaTypes.DockerManifestV2);

        Assert.Equal(MediaTypes.DockerLayerGzip, builder.LayerMediaType);
        Assert.Equal(MediaTypes.DockerImageConfigV1, builder.ConfigMediaType);
        Assert.Equal(MediaTypes.DockerManifestV2, builder.ManifestMediaType);

        // A layer built as OCI is coerced to the Docker layer media type in the manifest.
        builder.AddLayer(FakeLayer(MediaTypes.OciLayerGzipV1));
        BuiltImage built = builder.Build();
        Manifest manifest = Manifest.Parse(System.Text.Encoding.UTF8.GetString(built.ManifestBytes));
        Assert.Equal(MediaTypes.DockerLayerGzip, manifest.Layers[^1].MediaType);
    }
}
