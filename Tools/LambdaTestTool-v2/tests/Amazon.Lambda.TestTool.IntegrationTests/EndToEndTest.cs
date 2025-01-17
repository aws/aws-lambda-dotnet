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
    private readonly string _lambdaProjectPath;
    private readonly string _testToolProjectPath;

    private const string ApiGatewayPort = "5051";
    private const string LambdaPort = "5050";

    public ApiGatewayEmulatorProcessTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var testProjectDir = Path.GetFullPath("../../../../");
        _lambdaProjectPath = Path.GetFullPath(Path.Combine(testProjectDir, "LambdaTestFunction/src/LambdaTestFunction"));
        _testToolProjectPath = Path.GetFullPath(Path.Combine(testProjectDir, "../src/Amazon.Lambda.TestTool"));
    }

    [Theory]
    [InlineData("REST")]
    [InlineData("HTTPV1")]
    [InlineData("HTTPV2")]
    public async Task TestLambdaToUpper(string apiGatewayMode)
    {
        try
        {
            await StartTestToolProcess(apiGatewayMode);
            await WaitForGatewayHealthCheck();
            await StartLambdaProcess();

            var response = await TestEndpoint();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", response.Content);
        }
        finally
        {
            await CleanupProcesses();
        }
    }

    private async Task<(HttpStatusCode StatusCode, string Content)> TestEndpoint()
    {
        using var client = new HttpClient();
        var content = new StringContent("hello world", Encoding.UTF8, "text/plain");
        var response = await client.PostAsync($"http://localhost:{ApiGatewayPort}/testfunction", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, responseContent);
    }

    private async Task StartTestToolProcess(string apiGatewayMode)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{_testToolProjectPath}\" -- --api-gateway-emulator {apiGatewayMode} --no-launch-window",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.EnvironmentVariables["APIGATEWAY_EMULATOR_ROUTE_CONFIG"] = $@"{{
            ""LambdaResourceName"": ""testfunction"",
            ""Endpoint"": ""http://localhost:{LambdaPort}"",
            ""HttpMethod"": ""Post"",
            ""Path"": ""/testfunction""
        }}";

        _mainProcess = Process.Start(startInfo) ?? throw new Exception("Failed to start test tool process");
        ConfigureProcessLogging(_mainProcess, "TestTool");
    }

    private async Task StartLambdaProcess()
    {
        // Build the project
        var buildResult = await RunProcess("dotnet", "publish -c Release", _lambdaProjectPath);
        if (buildResult.ExitCode != 0)
        {
            throw new Exception($"Build failed: {buildResult.Output}\n{buildResult.Error}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = Path.Combine("bin", "Release", "net8.0", "win-x64", "publish", "LambdaTestFunction.dll"),
            WorkingDirectory = _lambdaProjectPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.EnvironmentVariables["AWS_LAMBDA_RUNTIME_API"] = $"localhost:{LambdaPort}/testfunction";
        startInfo.EnvironmentVariables["LAMBDA_TASK_ROOT"] = _lambdaProjectPath;
        startInfo.EnvironmentVariables["AWS_LAMBDA_FUNCTION_MEMORY_SIZE"] = "256";
        startInfo.EnvironmentVariables["AWS_LAMBDA_FUNCTION_TIMEOUT"] = "30";
        startInfo.EnvironmentVariables["AWS_LAMBDA_FUNCTION_NAME"] = "test-function";
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
