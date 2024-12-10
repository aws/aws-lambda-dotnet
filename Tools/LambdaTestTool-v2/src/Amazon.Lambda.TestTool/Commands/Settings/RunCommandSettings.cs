using Amazon.Lambda.TestTool.Models;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Amazon.Lambda.TestTool.Commands.Settings;

/// <summary>
/// Represents the settings for configuring the <see cref="RunCommand"/>, which is the default command.
/// </summary>
public sealed class RunCommandSettings : CommandSettings
{
    /// <summary>
    /// The hostname or IP address used for the test tool's web interface. 
    /// Any host other than an explicit IP address or localhost (e.g. '*', '+' or 'example.com') binds to all public IPv4 and IPv6 addresses.
    /// </summary>
    [CommandOption("--host <HOST>")]
    [Description(
        "The hostname or IP address used for the test tool's web interface. Any host other than an explicit IP address or localhost (e.g. '*', '+' or 'example.com') binds to all public IPv4 and IPv6 addresses.")]
    [DefaultValue(Constants.DefaultHost)]
    public string Host { get; set; } = Constants.DefaultHost;

    /// <summary>
    /// The port number used for the test tool's web interface.
    /// </summary>
    [CommandOption("-p|--port <PORT>")]
    [Description("The port number used for the test tool's web interface.")]
    [DefaultValue(Constants.DefaultPort)]
    public int Port { get; set; } = Constants.DefaultPort;

    /// <summary>
    /// Disable auto launching the test tool's web interface in a browser.
    /// </summary>
    [CommandOption("--no-launch-window")]
    [Description("Disable auto launching the test tool's web interface in a browser.")]
    public bool NoLaunchWindow { get; set; }

    /// <summary>
    /// If set to true the test tool will pause waiting for a key input before exiting. 
    /// The is useful when executing from an IDE so you can avoid having the output window immediately disappear after executing the Lambda code. 
    /// The default value is true.
    /// </summary>
    [CommandOption("--pause-exit")]
    [Description("If set to true the test tool will pause waiting for a key input before exiting. The is useful when executing from an IDE so you can avoid having the output window immediately disappear after executing the Lambda code. The default value is true.")]
    public bool PauseExit { get; set; }

    /// <summary>
    /// Disables logging in the application
    /// </summary>
    [CommandOption("--disable-logs")]
    [Description("Disables logging in the application")]
    public bool DisableLogs { get; set; }

    /// <summary>
    /// The API Gateway Emulator Mode specifies the format of the event that API Gateway sends to a Lambda integration, 
    /// and how API Gateway interprets the response from Lambda.
    /// The available modes are: Rest, HttpV1, HttpV2.
    /// </summary>
    [CommandOption("--api-gateway-emulator <MODE>")]
    [Description(
        "The API Gateway Emulator Mode specifies the format of the event that API Gateway sends to a Lambda integration, and how API Gateway interprets the response from Lambda. " +
        "The available modes are: Rest, HttpV1, HttpV2.")]
    public ApiGatewayEmulatorMode? ApiGatewayEmulatorMode { get; set; }

    /// <summary>
    /// The port number used for the test tool's API Gateway emulator.
    /// </summary>
    [CommandOption("--api-gateway-emulator-port <PORT>")]
    [Description("The port number used for the test tool's API Gateway emulator.")]
    [DefaultValue(Constants.DefaultApiGatewayEmulatorPort)]
    public int? ApiGatewayEmulatorPort { get; set; } = Constants.DefaultApiGatewayEmulatorPort;
}