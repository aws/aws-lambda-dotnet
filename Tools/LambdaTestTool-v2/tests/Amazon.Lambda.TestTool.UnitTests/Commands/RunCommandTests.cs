using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Spectre.Console.Cli;
using Moq;
using Amazon.Lambda.TestTool.UnitTests.Helpers;

namespace Amazon.Lambda.TestTool.UnitTests.Commands;

public class RunCommandTests
{
    private readonly Mock<IToolInteractiveService> _mockInteractiveService = new Mock<IToolInteractiveService>();
    private readonly Mock<IRemainingArguments> _mockRemainingArgs = new Mock<IRemainingArguments>();

    [Fact]
    public async Task ExecuteAsync_LambdaRuntimeApi_SuccessfulLaunch()
    {
        // Arrange
        var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(5000);
        var settings = new RunCommandSettings { NoLaunchWindow = true };
        var command = new RunCommand(_mockInteractiveService.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        var apiUrl = $"http://{settings.Host}:{settings.Port}";

        // Act
        var runningTask = command.ExecuteAsync(context, settings, cancellationSource);
        var isApiRunning = await TestHelpers.WaitForApiToStartAsync(apiUrl);
        cancellationSource.Cancel();

        // Assert
        var result = await runningTask;
        Assert.Equal(CommandReturnCodes.Success, result);
        Assert.True(isApiRunning);
    }

    [Fact]
    public async Task ExecuteAsync_ApiGatewayEmulator_SuccessfulLaunch()
    {
        // Arrange
        var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(5000);
        var settings = new RunCommandSettings { ApiGatewayEmulatorMode = ApiGatewayEmulatorMode.HttpV2, NoLaunchWindow = true};
        var command = new RunCommand(_mockInteractiveService.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        var apiUrl = $"http://{settings.Host}:{settings.ApiGatewayEmulatorPort}/health";

        // Act
        var runningTask = command.ExecuteAsync(context, settings, cancellationSource);
        var isApiRunning = await TestHelpers.WaitForApiToStartAsync(apiUrl);
        cancellationSource.Cancel();

        // Assert
        var result = await runningTask;
        Assert.Equal(CommandReturnCodes.Success, result);
        Assert.True(isApiRunning);
    }
}