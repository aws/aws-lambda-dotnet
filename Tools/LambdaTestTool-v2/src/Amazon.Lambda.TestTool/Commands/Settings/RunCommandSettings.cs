// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;
using Spectre.Console.Cli;
using System.ComponentModel;
using ValidationResult = Spectre.Console.ValidationResult;

namespace Amazon.Lambda.TestTool.Commands.Settings;

/// <summary>
/// Represents the settings for configuring the <see cref="RunCommand"/>.
/// </summary>
public sealed class RunCommandSettings : CommandSettings
{
    /// <summary>
    /// The hostname or IP address used for the test tool's web interface.
    /// Any host other than an explicit IP address or localhost (e.g. '*', '+' or 'example.com') binds to all public IPv4 and IPv6 addresses.
    /// </summary>
    [CommandOption("--lambda-emulator-host <HOST>")]
    [Description(
        "The hostname or IP address used for the test tool's web interface. Any host other than an explicit IP address or localhost (e.g. '*', '+' or 'example.com') binds to all public IPv4 and IPv6 addresses.")]
    [DefaultValue(Constants.DefaultLambdaEmulatorHost)]
    public string LambdaEmulatorHost { get; set; } = Constants.DefaultLambdaEmulatorHost;

    /// <summary>
    /// The port number used for the test tool's web interface. If a port is specified the Lambda emulator will be started.
    /// </summary>
    [CommandOption("-p|--lambda-emulator-port <PORT>")]
    [Description("The port number used for the test tool's web interface.")]
    public int? LambdaEmulatorPort { get; set; }

    /// <summary>
    /// The https port number used for the test tool's web interface. This is only used for the web UI. Lambda functions making REST calls to the Lambda Runtime API
    /// always use http configured by the port specified in <see cref="LambdaEmulatorPort"/>. To use HTTPS the environment must be configured with certs
    /// for the host specified in <see cref="LambdaEmulatorHost"/>.
    /// </summary>
    [CommandOption("--lambda-emulator-https-port <PORT>")]
    [Description("The https port number used for the test tool's web interface.")]
    public int? LambdaEmulatorHttpsPort { get; set; }

    /// <summary>
    /// Disable auto launching the test tool's web interface in a browser.
    /// </summary>
    [CommandOption("--no-launch-window")]
    [Description("Disable auto launching the test tool's web interface in a browser.")]
    public bool NoLaunchWindow { get; set; }

    /// <summary>
    /// The API Gateway Emulator Mode specifies the format of the event that API Gateway sends to a Lambda integration,
    /// and how API Gateway interprets the response from Lambda.
    /// The available modes are: Rest, HttpV1, HttpV2.
    /// </summary>
    [CommandOption("--api-gateway-emulator-mode <MODE>")]
    [Description(
        "The API Gateway Emulator Mode specifies the format of the event that API Gateway sends to a Lambda integration, and how API Gateway interprets the response from Lambda. " +
        "The available modes are: Rest, HttpV1, HttpV2.")]
    public ApiGatewayEmulatorMode? ApiGatewayEmulatorMode { get; set; }

    /// <summary>
    /// The port number used for the test tool's API Gateway emulator. If a port is specified the API Gateway emulator will be started. The --api-gateway-emulator-mode
    /// must also be set when setting the API Gateway emulator port.
    /// </summary>
    [CommandOption("--api-gateway-emulator-port <PORT>")]
    [Description("The port number used for the test tool's API Gateway emulator.")]
    public int? ApiGatewayEmulatorPort { get; set; }

    /// <summary>
    /// The https port number used for the test tool's API Gateway emulator. If a port is specified the API Gateway emulator will be started. The --api-gateway--emulator-mode must
    /// also be set when setting the API Gateway emulator port. To use HTTPS the environment must be configured with certs
    /// for the host specified in <see cref="LambdaEmulatorHost"/>.
    /// </summary>
    [CommandOption("--api-gateway-emulator-https-port <PORT>")]
    [Description("The https port number used for the test tool's API Gateway emulator.")]
    public int? ApiGatewayEmulatorHttpsPort { get; set; }

    /// <summary>
    /// The configuration for the SQS event source. The format of the config is a comma delimited key pairs. For example \"QueueUrl=queue-url,FunctionName=function-name,VisibilityTimeout=100\".
    /// Possible keys are: BatchSize, DisableMessageDelete, FunctionName, LambdaRuntimeApi, Profile, QueueUrl, Region, VisibilityTimeout
    /// </summary>
    [CommandOption("--sqs-eventsource-config <CONFIG>")]
    [Description("The configuration for the SQS event source. The format of the config is a comma delimited key pairs. For example \"QueueUrl=<queue-url>,FunctionName=<function-name>,VisibilityTimeout=100\". Possible keys are: BatchSize, DisableMessageDelete, FunctionName, LambdaRuntimeApi, Profile, QueueUrl, Region, VisibilityTimeout")]
    public string? SQSEventSourceConfig { get; set; }

    /// <summary>
    /// The absolute path used to save global settings and saved requests. You will need to specify a path in order to enable saving global settings and requests.
    /// </summary>
    [CommandOption("--config-storage-path <CONFIG-STORAGE-PATH>")]
    [Description("The absolute path used to save global settings and saved requests. You will need to specify a path in order to enable saving global settings and requests.")]
    public string? ConfigStoragePath { get; set; }

    /// <summary>
    /// Validate that <see cref="ConfigStoragePath"/> is an absolute path.
    /// </summary>
    public override ValidationResult Validate()
    {
        if (string.IsNullOrEmpty(ConfigStoragePath))
            return ValidationResult.Success();

        if (!Path.IsPathFullyQualified(ConfigStoragePath))
        {
            ConfigStoragePath = Path.Combine(Environment.CurrentDirectory, ConfigStoragePath);
        }

        return !Path.IsPathFullyQualified(ConfigStoragePath)
            ? ValidationResult.Error("'Config storage path' must be an absolute path.")
            : ValidationResult.Success();
    }
}
