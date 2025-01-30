// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Text;
using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.IO;
using Amazon.Lambda.TestTool.Tests.Common.Helpers;
using Moq;
using Spectre.Console.Cli;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.TestTool.IntegrationTests;

public class ApiGatewayEmulatorProcessTests : IAsyncDisposable
{
    private readonly Mock<IEnvironmentManager> _mockEnvironmentManager = new Mock<IEnvironmentManager>();
    private readonly Mock<IToolInteractiveService> _mockInteractiveService = new Mock<IToolInteractiveService>();
    private readonly Mock<IRemainingArguments> _mockRemainingArgs = new Mock<IRemainingArguments>();
    private readonly ITestOutputHelper _testOutputHelper;
    private Process? _lambdaProcess;

    public ApiGatewayEmulatorProcessTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestLambdaToUpperV2()
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var apiGatewayPort = TestHelpers.GetNextApiGatewayPort();
        var testProjectDir = Path.GetFullPath("../../../../../testapps");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaTestFunctionV2")),
            FunctionName = "LambdaTestFunctionV2",
            RouteName = "testfunction",
            HttpMethod = "Post"
        };

        var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            StartTestToolProcess(ApiGatewayEmulatorMode.HttpV2, config, lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);
            await StartLambdaProcess(config, lambdaPort);

            var response = await TestEndpoint(config, apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    [Fact]
    public async Task TestLambdaToUpperRest()
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var apiGatewayPort = TestHelpers.GetNextApiGatewayPort();
        var testProjectDir = Path.GetFullPath("../../../../../testapps");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaTestFunctionV1")),
            FunctionName = "LambdaTestFunctionV1",
            RouteName = "testfunction",
            HttpMethod = "Post"
        };

        var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            StartTestToolProcess(ApiGatewayEmulatorMode.Rest, config, lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);
            await StartLambdaProcess(config, lambdaPort);

            var response = await TestEndpoint(config, apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    [Fact]
    public async Task TestLambdaToUpperV1()
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var apiGatewayPort = TestHelpers.GetNextApiGatewayPort();
        var testProjectDir = Path.GetFullPath("../../../../../testapps");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaTestFunctionV1")),
            FunctionName = "LambdaTestFunctionV1",
            RouteName = "testfunction",
            HttpMethod = "Post"
        };

        var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            StartTestToolProcess(ApiGatewayEmulatorMode.HttpV1, config, lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);
            await StartLambdaProcess(config, lambdaPort);

            var response = await TestEndpoint(config, apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    [Fact]
    public async Task TestLambdaBinaryResponse()
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var apiGatewayPort = TestHelpers.GetNextApiGatewayPort();
        var testProjectDir = Path.GetFullPath("../../../../../testapps");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaBinaryFunction")),
            FunctionName = "LambdaBinaryFunction",
            RouteName = "binaryfunction",
            HttpMethod = "Get"
        };

        var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            StartTestToolProcess(ApiGatewayEmulatorMode.HttpV2, config, lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);
            await StartLambdaProcess(config, lambdaPort);

            var response = await TestEndpoint(config, apiGatewayPort);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);

            var binaryData = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(256, binaryData.Length);
            for (var i = 0; i < 256; i++)
            {
                Assert.Equal((byte)i, binaryData[i]);
            }
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    [Fact]
    public async Task TestLambdaReturnString()
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var apiGatewayPort = TestHelpers.GetNextApiGatewayPort();
        var testProjectDir = Path.GetFullPath("../../../../../testapps");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaReturnStringFunction")),
            FunctionName = "LambdaReturnStringFunction",
            RouteName = "stringfunction",
            HttpMethod = "Post"
        };

        var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            StartTestToolProcess(ApiGatewayEmulatorMode.HttpV2, config, lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);
            await StartLambdaProcess(config, lambdaPort);

            var response = await TestEndpoint(config, apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    [Fact]
    public async Task TestLambdaWithNullEndpoint()
    {
        var testProjectDir = Path.GetFullPath("../../../../../testapps");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaTestFunctionV2")),
            FunctionName = "LambdaTestFunctionV2",
            RouteName = "testfunction",
            HttpMethod = "Post"
        };

        var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
            var apiGatewayPort = TestHelpers.GetNextApiGatewayPort();
            StartTestToolProcessWithNullEndpoint(ApiGatewayEmulatorMode.HttpV2, lambdaPort, apiGatewayPort, config, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);
            await StartLambdaProcess(config, lambdaPort);

            var response = await TestEndpoint(config, apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    private record TestConfig
    {
        public required string TestToolPath { get; init; }
        public required string LambdaPath { get; init; }
        public required string FunctionName { get; init; }
        public required string RouteName { get; init; }
        public required string HttpMethod { get; init; }
    }

    private async Task<HttpResponseMessage> TestEndpoint(TestConfig config, int apiGatewayPort, HttpContent? content = null)
    {
        using var client = new HttpClient();
        return config.HttpMethod.ToUpper() switch
        {
            "POST" => await client.PostAsync($"http://localhost:{apiGatewayPort}/{config.RouteName}",
                content ?? new StringContent("hello world", Encoding.UTF8, "text/plain")),
            "GET" => await client.GetAsync($"http://localhost:{apiGatewayPort}/{config.RouteName}"),
            _ => throw new ArgumentException($"Unsupported HTTP method: {config.HttpMethod}")
        };
    }

    private void StartTestToolProcessWithNullEndpoint(ApiGatewayEmulatorMode apiGatewayMode, int lambdaPort, int apiGatewayPort, TestConfig config, CancellationTokenSource cancellationTokenSource)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("APIGATEWAY_EMULATOR_ROUTE_CONFIG", $@"{{
        ""LambdaResourceName"": ""{config.RouteName}"",
        ""HttpMethod"": ""{config.HttpMethod}"",
        ""Path"": ""/{config.RouteName}""
    }}");

        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(60));
        var settings = new RunCommandSettings { NoLaunchWindow = true, ApiGatewayEmulatorMode = apiGatewayMode, ApiGatewayEmulatorPort = apiGatewayPort, LambdaEmulatorPort = lambdaPort};

        var command = new RunCommand(_mockInteractiveService.Object, _mockEnvironmentManager.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);

        _ = command.ExecuteAsync(context, settings, cancellationTokenSource);
    }

    private void StartTestToolProcess(ApiGatewayEmulatorMode apiGatewayMode, TestConfig config, int lambdaPort, int apiGatewayPort, CancellationTokenSource cancellationTokenSource)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("APIGATEWAY_EMULATOR_ROUTE_CONFIG", $@"{{
            ""LambdaResourceName"": ""{config.RouteName}"",
            ""Endpoint"": ""http://localhost:{lambdaPort}"",
            ""HttpMethod"": ""{config.HttpMethod}"",
            ""Path"": ""/{config.RouteName}""
        }}");
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(60));
        var settings = new RunCommandSettings { LambdaEmulatorPort = lambdaPort, NoLaunchWindow = true, ApiGatewayEmulatorMode = apiGatewayMode,ApiGatewayEmulatorPort = apiGatewayPort};
        var command = new RunCommand(_mockInteractiveService.Object, _mockEnvironmentManager.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);

        // Act
        _ = command.ExecuteAsync(context, settings, cancellationTokenSource);
    }

    private async Task StartLambdaProcess(TestConfig config, int lambdaPort)
    {
        // Build the project
        var buildResult = await RunProcess("dotnet", "publish -c Release", config.LambdaPath);
        if (buildResult.ExitCode != 0)
        {
            throw new Exception($"Build failed: {buildResult.Output}\n{buildResult.Error}");
        }

        var publishFolder = Path.Combine(config.LambdaPath, "bin", "Release", "net8.0");
        var archFolders = Directory.GetDirectories(publishFolder, "*");
        var archFolder = Assert.Single(archFolders);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = Path.Combine(archFolder, "publish", $"{config.FunctionName}.dll"),
            WorkingDirectory = config.LambdaPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.EnvironmentVariables["AWS_LAMBDA_RUNTIME_API"] = $"localhost:{lambdaPort}/{config.RouteName}";
        startInfo.EnvironmentVariables["LAMBDA_TASK_ROOT"] = config.LambdaPath;
        startInfo.EnvironmentVariables["AWS_LAMBDA_FUNCTION_MEMORY_SIZE"] = "256";
        startInfo.EnvironmentVariables["AWS_LAMBDA_FUNCTION_TIMEOUT"] = "30";
        startInfo.EnvironmentVariables["AWS_LAMBDA_FUNCTION_NAME"] = config.FunctionName;
        startInfo.EnvironmentVariables["AWS_LAMBDA_FUNCTION_VERSION"] = "$LATEST";

        _lambdaProcess = Process.Start(startInfo) ?? throw new Exception("Failed to start Lambda process");
        ConfigureProcessLogging(_lambdaProcess, "Lambda");
    }

    private void ConfigureProcessLogging(Process process, string prefix)
    {
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) LogMessage($"{prefix}: {e.Data}");
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) LogMessage($"{prefix} Error: {e.Data}");
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private async Task<(int ExitCode, string Output, string Error)> RunProcess(string fileName, string arguments, string? workingDirectory = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (process.ExitCode, output.ToString(), error.ToString());
    }

    private async Task WaitForGatewayHealthCheck(int apiGatewayPort)
    {
        using var client = new HttpClient();
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(10);
        var healthUrl = $"http://localhost:{apiGatewayPort}/__lambda_test_tool_apigateway_health__";

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                var response = await client.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    LogMessage("API Gateway health check succeeded");
                    return;
                }
            }
            catch
            {
                await Task.Delay(100);
            }
        }
        throw new TimeoutException("API Gateway failed to start within timeout period");
    }

    private void LogMessage(string message)
    {
        Console.WriteLine(message);
        _testOutputHelper.WriteLine(message);
    }

    private async Task CleanupProcesses()
    {
        var processes = new[] { _lambdaProcess };
        foreach (var process in processes.Where(p => p != null && !p.HasExited))
        {
            try
            {
                process!.Kill(entireProcessTree: true);
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                LogMessage($"Error killing process: {ex.Message}");
            }
            finally
            {
                process!.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupProcesses();
    }
}
