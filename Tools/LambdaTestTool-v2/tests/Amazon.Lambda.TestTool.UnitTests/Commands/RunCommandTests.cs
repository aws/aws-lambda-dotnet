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

namespace Amazon.Lambda.TestTool.UnitTests.Commands;

public class RunCommandTests
{
    private readonly Mock<IToolInteractiveService> _mockInteractiveService = new Mock<IToolInteractiveService>();
    private readonly Mock<IRemainingArguments> _mockRemainingArgs = new Mock<IRemainingArguments>();

    [Fact]
    public async Task ExecuteAsync_LambdaRuntimeApi_SuccessfulLaunch()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(5000);
        var settings = new RunCommandSettings { Port = 9001, NoLaunchWindow = true };
        var command = new RunCommand(_mockInteractiveService.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        var apiUrl = $"http://{settings.Host}:{settings.Port}";

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
        var settings = new RunCommandSettings { Port = 9002,  ApiGatewayEmulatorMode = ApiGatewayEmulatorMode.HttpV2, NoLaunchWindow = true};
        var command = new RunCommand(_mockInteractiveService.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        var apiUrl = $"http://{settings.Host}:{settings.ApiGatewayEmulatorPort}/__lambda_test_tool_apigateway_health__";

        // Act
        var runningTask = command.ExecuteAsync(context, settings, cancellationSource);
        var isApiRunning = await TestHelpers.WaitForApiToStartAsync(apiUrl);
        await cancellationSource.CancelAsync();

        // Assert
        var result = await runningTask;
        Assert.Equal(CommandReturnCodes.Success, result);
        Assert.True(isApiRunning);
    }
}
