using System.Text.Json;
using System.Text.Json.Nodes;

namespace Amazon.OCI.Builder.Images;

/// <summary>
/// A mutable view over an OCI / Docker image configuration JSON document — the object referenced by an image
/// manifest's <c>config</c> descriptor. It carries the execution parameters (entrypoint, env, working directory,
/// user, exposed ports, labels) and the <c>rootfs.diff_ids</c> / <c>history</c> that describe the layer stack.
/// </summary>
/// <remarks>
/// <para>
/// The image config schema has many fields and registries reject an image whose config digest does not match its
/// bytes, so this type is deliberately a thin wrapper over a <see cref="JsonObject"/> of the <em>original</em>
/// base-image config rather than a hand-modeled POCO. Preserving every field the base set (even ones this library
/// does not understand) keeps the derived image valid and avoids silently dropping data. Mutations touch only the
/// specific nodes they need.
/// </para>
/// <para>
/// Call <see cref="ToJsonBytes"/> to get the canonical bytes to upload; the config digest is the SHA-256 of those
/// bytes. All serialization is compact (no insignificant whitespace) and preserves insertion order so the output
/// is stable.
/// </para>
/// </remarks>
public sealed class ImageConfig
{
    // Writer options for the canonical config bytes: compact (no insignificant whitespace) and relaxed escaping,
    // matching what registries emit. The config digest is computed over these exact bytes, so this formatting is
    // part of the contract. Writing the JsonNode tree through a Utf8JsonWriter keeps serialization trim/AOT-safe
    // (no reflection-based JsonSerializer overload or DefaultJsonTypeInfoResolver).
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly JsonObject _root;

    private ImageConfig(JsonObject root) => _root = root;

    /// <summary>Parses an existing image config document (typically the base image's config blob).</summary>
    /// <param name="json">The config JSON.</param>
    /// <exception cref="ArgumentException">The JSON is null, empty, or not a JSON object.</exception>
    public static ImageConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("The image config JSON must not be null or empty.", nameof(json));
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The image config is not valid JSON.", nameof(json), ex);
        }

        if (node is not JsonObject obj)
        {
            throw new ArgumentException("The image config JSON must be a JSON object.", nameof(json));
        }

        return new ImageConfig(obj);
    }

    /// <summary>The OS this image targets (the config's <c>os</c> field), for example <c>linux</c>.</summary>
    public string? Os => _root["os"]?.GetValue<string>();

    /// <summary>The CPU architecture this image targets (the config's <c>architecture</c> field), for example <c>amd64</c>.</summary>
    public string? Architecture => _root["architecture"]?.GetValue<string>();

    /// <summary>
    /// Appends a new layer to the config: adds its <paramref name="diffId"/> to <c>rootfs.diff_ids</c> and records
    /// a matching <c>history</c> entry. This must be called for each layer added to the manifest, in the same order.
    /// </summary>
    /// <param name="diffId">The layer's uncompressed-tar digest (<see cref="Layer.DiffId"/>).</param>
    /// <param name="createdBy">An optional free-text description recorded in the history entry.</param>
    public void AddLayer(string diffId, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diffId);

        // rootfs.diff_ids — create the structure if the base config somehow lacked it.
        if (_root["rootfs"] is not JsonObject rootfs)
        {
            rootfs = new JsonObject { ["type"] = "layers" };
            _root["rootfs"] = rootfs;
        }
        if (rootfs["diff_ids"] is not JsonArray diffIds)
        {
            diffIds = new JsonArray();
            rootfs["diff_ids"] = diffIds;
        }
        // Add the diff_id as a JsonNode (not the generic Add<string>, which is trim/AOT-unsafe).
        diffIds.Add(JsonValue.Create(diffId) as JsonNode);

        // history — one entry per layer keeps the array aligned with the layer stack. The timestamp is fixed so
        // the config stays reproducible (see Layer's reproducibility rationale).
        if (_root["history"] is not JsonArray history)
        {
            history = new JsonArray();
            _root["history"] = history;
        }
        var entry = new JsonObject
        {
            ["created"] = "1970-01-01T00:00:00Z",
        };
        if (!string.IsNullOrEmpty(createdBy))
        {
            entry["created_by"] = createdBy;
        }
        history.Add(entry as JsonNode);
    }

    /// <summary>Sets the container entrypoint (the config's <c>config.Entrypoint</c>).</summary>
    public void SetEntrypoint(params string[] entrypoint)
        => SetConfigArray("Entrypoint", entrypoint);

    /// <summary>Sets the container default command/arguments (the config's <c>config.Cmd</c>).</summary>
    public void SetCmd(params string[] cmd)
        => SetConfigArray("Cmd", cmd);

    /// <summary>Sets the working directory (the config's <c>config.WorkingDir</c>).</summary>
    public void SetWorkingDirectory(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ConfigObject()["WorkingDir"] = workingDirectory;
    }

    /// <summary>Sets the user the container runs as (the config's <c>config.User</c>).</summary>
    public void SetUser(string user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        ConfigObject()["User"] = user;
    }

    /// <summary>
    /// Adds or overwrites an environment variable (merged into the config's <c>config.Env</c>, which is an array of
    /// <c>KEY=value</c> strings). An existing entry for the same key is replaced in place.
    /// </summary>
    public void SetEnvironmentVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        JsonObject config = ConfigObject();
        if (config["Env"] is not JsonArray env)
        {
            env = new JsonArray();
            config["Env"] = env;
        }

        string prefix = name + "=";
        string entry = prefix + value;
        for (int i = 0; i < env.Count; i++)
        {
            if (env[i]?.GetValue<string>() is { } existing && existing.StartsWith(prefix, StringComparison.Ordinal))
            {
                env[i] = entry;
                return;
            }
        }
        env.Add(JsonValue.Create(entry) as JsonNode);
    }

    /// <summary>Adds or overwrites a label (the config's <c>config.Labels</c> map).</summary>
    public void SetLabel(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        JsonObject config = ConfigObject();
        if (config["Labels"] is not JsonObject labels)
        {
            labels = new JsonObject();
            config["Labels"] = labels;
        }
        labels[key] = value;
    }

    /// <summary>Marks a port as exposed (added to the config's <c>config.ExposedPorts</c> set).</summary>
    /// <param name="port">A port spec such as <c>8080/tcp</c>; a bare number is treated as <c>/tcp</c>.</param>
    public void ExposePort(string port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(port);
        string key = port.Contains('/', StringComparison.Ordinal) ? port : port + "/tcp";

        JsonObject config = ConfigObject();
        if (config["ExposedPorts"] is not JsonObject ports)
        {
            ports = new JsonObject();
            config["ExposedPorts"] = ports;
        }
        // The value is an empty object per the image-spec convention.
        ports[key] = new JsonObject();
    }

    /// <summary>Serializes the config to its canonical, compact UTF-8 bytes (what the config digest is computed over).</summary>
    public byte[] ToJsonBytes()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            _root.WriteTo(writer);
        }
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Serializes the config to its canonical, compact JSON string.</summary>
    public string ToJsonString() => System.Text.Encoding.UTF8.GetString(ToJsonBytes());

    /// <summary>Returns the <c>config</c> sub-object, creating it if absent.</summary>
    private JsonObject ConfigObject()
    {
        if (_root["config"] is not JsonObject config)
        {
            config = new JsonObject();
            _root["config"] = config;
        }
        return config;
    }

    private void SetConfigArray(string key, string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
        {
            throw new ArgumentException($"'{key}' must contain at least one element.", nameof(values));
        }

        var array = new JsonArray();
        foreach (string value in values)
        {
            array.Add(JsonValue.Create(value) as JsonNode);
        }
        ConfigObject()[key] = array;
    }
}
