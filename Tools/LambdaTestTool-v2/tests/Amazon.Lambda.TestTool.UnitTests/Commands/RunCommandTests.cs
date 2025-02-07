// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Spectre.Console.Cli;
using Moq;
using Amazon.Lambda.TestTool.UnitTests.Helpers;
using Xunit;
using Amazon.Lambda.TestTool.Services.IO;
using Amazon.Lambda.TestTool.Utilities;
using System.Text.Json.Nodes;

namespace Amazon.Lambda.TestTool.UnitTests.Commands;

public class RunCommandTests
{
    private readonly Mock<IEnvironmentManager> _mockEnvironmentManager = new Mock<IEnvironmentManager>();
    private readonly Mock<IToolInteractiveService> _mockInteractiveService = new Mock<IToolInteractiveService>();
    private readonly Mock<IRemainingArguments> _mockRemainingArgs = new Mock<IRemainingArguments>();

    [Fact]
    public async Task ExecuteAsync_LambdaRuntimeApi_SuccessfulLaunch()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(5000);
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var settings = new RunCommandSettings { LambdaEmulatorPort = lambdaPort, NoLaunchWindow = true };
        var command = new RunCommand(_mockInteractiveService.Object, _mockEnvironmentManager.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        var apiUrl = $"http://{settings.LambdaEmulatorHost}:{settings.LambdaEmulatorPort}";

        // Act
        var runningTask = command.ExecuteAsync(context, settings, cancellationSource);
        var isApiRunning = await TestHelpers.WaitForApiToStartAsync(apiUrl);
        await cancellationSource.CancelAsync();

        // Assert
        var result = await runningTask;
        Assert.Equal(CommandReturnCodes.Success, result);
        Assert.True(isApiRunning);
    }

    [Fact]
    public async Task ExecuteAsync_ApiGatewayEmulator_SuccessfulLaunch()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(5000);
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var gatewayPort = TestHelpers.GetNextApiGatewayPort();
        var settings = new RunCommandSettings { LambdaEmulatorPort = lambdaPort, ApiGatewayEmulatorPort = gatewayPort, ApiGatewayEmulatorMode = ApiGatewayEmulatorMode.HttpV2, NoLaunchWindow = true};
        var command = new RunCommand(_mockInteractiveService.Object, _mockEnvironmentManager.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        var apiUrl = $"http://{settings.LambdaEmulatorHost}:{settings.ApiGatewayEmulatorPort}/__lambda_test_tool_apigateway_health__";

        // Act
        var runningTask = command.ExecuteAsync(context, settings, cancellationSource);
        var isApiRunning = await TestHelpers.WaitForApiToStartAsync(apiUrl);
        await cancellationSource.CancelAsync();

        // Assert
        var result = await runningTask;
        Assert.Equal(CommandReturnCodes.Success, result);
        Assert.True(isApiRunning);
    }

    [Fact]
    public async Task ExecuteAsync_EnvPorts_SuccessfulLaunch()
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var gatewayPort = TestHelpers.GetNextApiGatewayPort();

        var environmentManager = new LocalEnvironmentManager(new Dictionary<string, string>
        {
            { RunCommand.LAMBDA_RUNTIME_API_PORT, $"{lambdaPort}" },
            { RunCommand.API_GATEWAY_EMULATOR_PORT, $"{gatewayPort}" }
        });

        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(5000);
        var settings = new RunCommandSettings { ApiGatewayEmulatorMode = ApiGatewayEmulatorMode.HttpV2, NoLaunchWindow = true };
        var command = new RunCommand(_mockInteractiveService.Object, environmentManager);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        var apiUrl = $"http://{settings.LambdaEmulatorHost}:{gatewayPort}/__lambda_test_tool_apigateway_health__";

        // Act
        var runningTask = command.ExecuteAsync(context, settings, cancellationSource);
        var isApiRunning = await TestHelpers.WaitForApiToStartAsync(apiUrl);
        await cancellationSource.CancelAsync();

        // Assert
        var result = await runningTask;
        Assert.Equal(CommandReturnCodes.Success, result);
        Assert.True(isApiRunning);
    }

    [Fact]
    public void VerifyToolInfo()
    {
        var writeCalls = 0;
        string? versionInfo = null;
        Mock<IToolInteractiveService> mockInteractiveService = new Mock<IToolInteractiveService>();
        mockInteractiveService.Setup(i => i.WriteLine(It.IsAny<string>()))
            .Callback((string message) =>
            {
                writeCalls++;
                versionInfo = message;
            });

        var settings = new ToolInfoCommandSettings { Format = ToolInfoCommandSettings.InfoFormat.Json };
        var command = new ToolInfoCommand(mockInteractiveService.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        command.Execute(context, settings);

        Assert.Equal(1, writeCalls);
        Assert.True(!string.IsNullOrEmpty(versionInfo));

        JsonNode? jsonNode = JsonNode.Parse(versionInfo);
        Assert.NotNull(jsonNode);

        var version = jsonNode["Version"]?.ToString();
        Assert.NotNull(version);

        // The Version.TryParse does not like the preview suffix
        version = version.Replace("-preview", "");
        Assert.True(Version.TryParse(version, out var _));

        var installPath = jsonNode["InstallPath"]?.ToString();
        Assert.NotNull(installPath);
        Assert.True(Directory.Exists(installPath));
        Assert.True(Path.IsPathFullyQualified(installPath));
    }
}
