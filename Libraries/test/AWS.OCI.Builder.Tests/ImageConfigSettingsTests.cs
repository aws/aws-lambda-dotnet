using System.Text.Json.Nodes;
using AWS.OCI.Builder.Images;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>
/// Reading the container settings a base image set, and how <see cref="ImageConfig.SetEntrypoint"/> and
/// <see cref="ImageConfig.SetCmd"/> interact with them, matching a Dockerfile's ENTRYPOINT and CMD.
/// </summary>
public class ImageConfigSettingsTests
{
    // A base image config with an entrypoint, command, environment and working directory, like a Lambda base image.
    private const string BaseConfig = """
        {"architecture":"arm64","os":"linux","rootfs":{"type":"layers","diff_ids":[]},
         "config":{"Entrypoint":["/lambda-entrypoint.sh"],"Cmd":["function.handler"],
                   "Env":["PATH=/usr/bin","LANG=en_US.UTF-8"],"WorkingDir":"/var/task"}}
        """;

    private static JsonObject ConfigSection(ImageConfig config)
        => (JsonObject)((JsonObject)JsonNode.Parse(config.ToJsonString())!)["config"]!;

    [Fact]
    public void Getters_return_the_base_image_settings()
    {
        ImageConfig config = ImageConfig.Parse(BaseConfig);

        Assert.Equal(new[] { "/lambda-entrypoint.sh" }, config.Entrypoint);
        Assert.Equal(new[] { "function.handler" }, config.Cmd);
        Assert.Equal(new[] { "PATH=/usr/bin", "LANG=en_US.UTF-8" }, config.EnvironmentVariables);
        Assert.Equal("/var/task", config.WorkingDirectory);
    }

    [Fact]
    public void Getters_return_null_when_a_setting_is_absent()
    {
        ImageConfig config = ImageConfig.Parse("""{"os":"linux","architecture":"amd64"}""");

        Assert.Null(config.Entrypoint);
        Assert.Null(config.Cmd);
        Assert.Null(config.EnvironmentVariables);
        Assert.Null(config.WorkingDirectory);
    }

    [Fact]
    public void Getters_reflect_values_set_in_this_build()
    {
        ImageConfig config = ImageConfig.Parse(BaseConfig);
        config.SetWorkingDirectory("/app");
        config.SetEnvironmentVariable("LANG", "C.UTF-8");

        Assert.Equal("/app", config.WorkingDirectory);
        Assert.Equal(new[] { "PATH=/usr/bin", "LANG=C.UTF-8" }, config.EnvironmentVariables);
    }

    [Fact]
    public void Non_string_values_from_the_base_image_are_skipped_rather_than_thrown()
    {
        // The config comes from a registry, so a malformed element must not make reading or editing it throw.
        ImageConfig config = ImageConfig.Parse("""
            {"os":"linux","config":{"Entrypoint":["/bin/sh",1,null],"Env":[{"x":1},"A=1",2],"WorkingDir":5}}
            """);

        Assert.Equal(new[] { "/bin/sh" }, config.Entrypoint);
        Assert.Equal(new[] { "A=1" }, config.EnvironmentVariables);
        Assert.Null(config.WorkingDirectory);

        config.SetEnvironmentVariable("A", "2");
        Assert.Equal(new[] { "A=2" }, config.EnvironmentVariables);
    }

    [Fact]
    public void SetEntrypoint_clears_a_cmd_inherited_from_the_base_image()
    {
        ImageConfig config = ImageConfig.Parse(BaseConfig);
        config.SetEntrypoint("/var/task/bootstrap");

        Assert.Equal(new[] { "/var/task/bootstrap" }, config.Entrypoint);
        Assert.Null(config.Cmd);
        Assert.False(ConfigSection(config).ContainsKey("Cmd"));
    }

    [Fact]
    public void SetEntrypoint_keeps_a_cmd_set_earlier_in_this_build()
    {
        ImageConfig config = ImageConfig.Parse(BaseConfig);
        config.SetCmd("--port", "8080");
        config.SetEntrypoint("/var/task/bootstrap");

        Assert.Equal(new[] { "/var/task/bootstrap" }, config.Entrypoint);
        Assert.Equal(new[] { "--port", "8080" }, config.Cmd);
    }

    [Fact]
    public void SetCmd_after_SetEntrypoint_sets_the_cmd()
    {
        ImageConfig config = ImageConfig.Parse(BaseConfig);
        config.SetEntrypoint("/var/task/bootstrap");
        config.SetCmd("handler");

        Assert.Equal(new[] { "handler" }, config.Cmd);
    }

    [Fact]
    public void SetCmd_with_no_arguments_clears_it_and_counts_as_set()
    {
        ImageConfig config = ImageConfig.Parse(BaseConfig);
        config.SetCmd();

        Assert.Null(config.Cmd);
        Assert.False(ConfigSection(config).ContainsKey("Cmd"));

        // A cleared Cmd is still this build's choice, so a later SetEntrypoint leaves it alone rather than
        // treating it as inherited (it is already absent either way).
        config.SetEntrypoint("/bin/app");
        Assert.Null(config.Cmd);
        Assert.Equal(new[] { "/bin/app" }, config.Entrypoint);
    }

    [Fact]
    public void SetEntrypoint_with_no_arguments_clears_it()
    {
        ImageConfig config = ImageConfig.Parse(BaseConfig);
        config.SetEntrypoint();

        Assert.Null(config.Entrypoint);
        Assert.False(ConfigSection(config).ContainsKey("Entrypoint"));
        Assert.Null(config.Cmd); // the inherited Cmd is cleared too, as with ENTRYPOINT []
    }

    [Fact]
    public void Null_arrays_are_rejected()
    {
        ImageConfig config = ImageConfig.Parse(BaseConfig);

        Assert.Throws<ArgumentNullException>(() => config.SetEntrypoint(null!));
        Assert.Throws<ArgumentNullException>(() => config.SetCmd(null!));
    }
}
