// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Text;
using Xunit.Abstractions;

namespace Amazon.Lambda.TestTool.IntegrationTests;

public class ApiGatewayEmulatorProcessTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private Process? _mainProcess;
    private Process? _lambdaProcess;

    private const string ApiGatewayPort = "5051";
    private const string LambdaPort = "5050";

    public ApiGatewayEmulatorProcessTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("HTTPV2")]
    public async Task TestLambdaToUpper(string apiGatewayMode)
    {
        var testProjectDir = Path.GetFullPath("../../../../");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaTestFunction/src/LambdaTestFunction")),
            FunctionName = "LambdaTestFunction",
            RouteName = "testfunction",
            HttpMethod = "Post"
        };

        try
        {
            await StartTestToolProcess(apiGatewayMode, config);
            await WaitForGatewayHealthCheck();
            await StartLambdaProcess(config);

            var response = await TestEndpoint(config);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await CleanupProcesses();
        }
    }

    [Theory]
    [InlineData("HTTPV2")]
    public async Task TestLambdaBinaryResponse(string apiGatewayMode)
    {
        var testProjectDir = Path.GetFullPath("../../../../");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaBinaryFunction/src/LambdaBinaryFunction")),
            FunctionName = "LambdaBinaryFunction",
            RouteName = "binaryfunction",
            HttpMethod = "Get"
        };

        try
        {
            await StartTestToolProcess(apiGatewayMode, config);
            await WaitForGatewayHealthCheck();
            await StartLambdaProcess(config);

            var response = await TestEndpoint(config);

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
            await CleanupProcesses();
        }
    }

    [Theory]
    [InlineData("HTTPV2")]
    public async Task TestLambdaReturnString(string apiGatewayMode)
    {
        var testProjectDir = Path.GetFullPath("../../../../");
        var config = new TestConfig
        {
            TestToolPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool")),
            LambdaPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaReturnStringFunction/src/LambdaReturnStringFunction")),
            FunctionName = "LambdaReturnStringFunction",
            RouteName = "stringfunction",
            HttpMethod = "Post"
        };

        try
        {
            await StartTestToolProcess(apiGatewayMode, config);
            await WaitForGatewayHealthCheck();
            await StartLambdaProcess(config);

            var response = await TestEndpoint(config);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("\"HELLO WORLD\"", responseContent);
        }
        finally
        {
            await CleanupProcesses();
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

    private async Task<HttpResponseMessage> TestEndpoint(TestConfig config, HttpContent? content = null)
    {
        using var client = new HttpClient();
        return config.HttpMethod.ToUpper() switch
        {
            "POST" => await client.PostAsync($"http://localhost:{ApiGatewayPort}/{config.RouteName}",
                content ?? new StringContent("hello world", Encoding.UTF8, "text/plain")),
            "GET" => await client.GetAsync($"http://localhost:{ApiGatewayPort}/{config.RouteName}"),
            _ => throw new ArgumentException($"Unsupported HTTP method: {config.HttpMethod}")
        };
    }

    private async Task StartTestToolProcess(string apiGatewayMode, TestConfig config)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{config.TestToolPath}\" -- --api-gateway-emulator {apiGatewayMode} --no-launch-window",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.EnvironmentVariables["APIGATEWAY_EMULATOR_ROUTE_CONFIG"] = $@"{{
            ""LambdaResourceName"": ""{config.RouteName}"",
            ""Endpoint"": ""http://localhost:{LambdaPort}"",
            ""HttpMethod"": ""{config.HttpMethod}"",
            ""Path"": ""/{config.RouteName}""
        }}";

        _mainProcess = Process.Start(startInfo) ?? throw new Exception("Failed to start test tool process");
        ConfigureProcessLogging(_mainProcess, "TestTool");
    }

    private async Task StartLambdaProcess(TestConfig config)
    {
        // Build the project
        var buildResult = await RunProcess("dotnet", "publish -c Release", config.LambdaPath);
        if (buildResult.ExitCode != 0)
        {
            throw new Exception($"Build failed: {buildResult.Output}\n{buildResult.Error}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = Path.Combine("bin", "Release", "net8.0", "win-x64", "publish", $"{config.FunctionName}.dll"),
            WorkingDirectory = config.LambdaPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.EnvironmentVariables["AWS_LAMBDA_RUNTIME_API"] = $"localhost:{LambdaPort}/{config.RouteName}";
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

    private async Task WaitForGatewayHealthCheck()
    {
        using var client = new HttpClient();
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(10);
        var healthUrl = $"http://localhost:{ApiGatewayPort}/__lambda_test_tool_apigateway_health__";

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
        var processes = new[] { _mainProcess, _lambdaProcess };
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
