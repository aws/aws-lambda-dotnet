// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.IO;
using Moq;
using Spectre.Console.Cli;
using Xunit.Abstractions;
using Amazon.Lambda.TestTool.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Amazon.SecurityToken;

namespace Amazon.Lambda.TestTool.IntegrationTests;

public abstract class BaseApiGatewayTest
{
    protected readonly ITestOutputHelper TestOutputHelper;
    protected readonly Mock<IToolInteractiveService> MockInteractiveService;
    protected readonly Mock<IEnvironmentManager> MockEnvironmentManager;
    protected readonly Mock<IRemainingArguments> MockRemainingArgs;
    protected CancellationTokenSource CancellationTokenSource;
    protected static readonly object TestLock = new();

    protected BaseApiGatewayTest(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;
        MockEnvironmentManager = new Mock<IEnvironmentManager>();
        MockInteractiveService = new Mock<IToolInteractiveService>();
        MockRemainingArgs = new Mock<IRemainingArguments>();
        CancellationTokenSource = new CancellationTokenSource();
    }

    protected async Task CleanupAsync()
    {
        if (CancellationTokenSource != null)
        {
            await CancellationTokenSource.CancelAsync();
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new CancellationTokenSource();
        }
    }

    protected async Task StartTestToolProcessAsync(ApiGatewayEmulatorMode apiGatewayMode, string routeName, int lambdaPort, int apiGatewayPort, CancellationTokenSource cancellationTokenSource, string httpMethod = "POST")
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("APIGATEWAY_EMULATOR_ROUTE_CONFIG", $@"{{
            ""LambdaResourceName"": ""{routeName}"",
            ""Endpoint"": ""http://localhost:{lambdaPort}"",
            ""HttpMethod"": ""{httpMethod}"",
            ""Path"": ""/{routeName}""
        }}");

        var settings = new RunCommandSettings
        {
            LambdaEmulatorPort = lambdaPort,
            NoLaunchWindow = true,
            ApiGatewayEmulatorMode = apiGatewayMode,
            ApiGatewayEmulatorPort = apiGatewayPort
        };

        var command = new RunCommand(MockInteractiveService.Object, MockEnvironmentManager.Object);
        var context = new CommandContext(new List<string>(), MockRemainingArgs.Object, "run", null);
        _ = command.ExecuteAsync(context, settings, cancellationTokenSource);

        await Task.Delay(2000, cancellationTokenSource.Token);
    }

    protected async Task WaitForGatewayHealthCheck(int apiGatewayPort)
    {
        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(60);
            var healthUrl = $"http://localhost:{apiGatewayPort}/__lambda_test_tool_apigateway_health__";

            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    var response = await client.GetAsync(healthUrl, new CancellationTokenSource(10000).Token);
                    if (response.IsSuccessStatusCode)
                    {
                        TestOutputHelper.WriteLine("API Gateway health check succeeded");
                        await Task.Delay(2000);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    TestOutputHelper.WriteLine($"Health check attempt failed: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
            throw new TimeoutException("API Gateway failed to start within timeout period");
        }
    }

    protected async Task<HttpResponseMessage> TestEndpoint(string routeName, int apiGatewayPort, string httpMethod = "POST", string? payload = null)
    {
        TestOutputHelper.WriteLine($"Testing endpoint: http://localhost:{apiGatewayPort}/{routeName}");
        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(120);

            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(180);
            Exception? lastException = null;

            while (DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(2000);

                try
                {
                    return httpMethod.ToUpper() switch
                    {
                        "POST" => await client.PostAsync(
                            $"http://localhost:{apiGatewayPort}/{routeName}",
                            new StringContent(payload ?? "hello world", Encoding.UTF8, new MediaTypeHeaderValue("text/plain")), new CancellationTokenSource(5000).Token),
                        "GET" => await client.GetAsync($"http://localhost:{apiGatewayPort}/{routeName}", new CancellationTokenSource(5000).Token),
                        _ => throw new ArgumentException($"Unsupported HTTP method: {httpMethod}")
                    };
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    TestOutputHelper.WriteLine($"Request attempt failed - Message: {ex.Message}");
                    TestOutputHelper.WriteLine($"Request attempt failed - Stack Trace: {ex.StackTrace}");
                    await Task.Delay(1000);
                }
            }

            throw new TimeoutException($"Failed to complete request within timeout period: {lastException?.Message}", lastException);
        }
    }

    protected (int lambdaPort, int apiGatewayPort) GetFreePorts()
    {
        var lambdaPort = GetFreePort();
        int apiGatewayPort;
        do
        {
            apiGatewayPort = GetFreePort();
        } while (apiGatewayPort == lambdaPort);

        return (lambdaPort, apiGatewayPort);
    }

    protected int GetFreePort()
    {
        Console.WriteLine("Looking for free port");
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.MapGet("/", () => "test");

        CancellationTokenSource tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(5000);
        var runTask = app.RunAsync(tokenSource.Token);
        var uri = new Uri(app.Urls.First());

        using var client = new HttpClient();
        string? content = null;

        Console.WriteLine($"Testing port: {uri.Port}");
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while(DateTime.UtcNow < timeout)
        {
            try
            {
                content = client.GetStringAsync(uri, tokenSource.Token).GetAwaiter().GetResult();
                Console.WriteLine("Port was successful");
                break;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }

        if (!string.Equals(content, "test"))
        {
            Console.WriteLine("Port test failed trying again");
            var recursivePort = GetFreePort();
            tokenSource.Cancel();
            return recursivePort;
        }

        tokenSource.Cancel();
        Task.Delay(2000);

        return uri.Port;
    }

    protected async Task StartTestToolProcessWithNullEndpoint(ApiGatewayEmulatorMode apiGatewayMode, string routeName, int lambdaPort, int apiGatewayPort, CancellationTokenSource cancellationTokenSource)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("APIGATEWAY_EMULATOR_ROUTE_CONFIG", $@"{{
            ""LambdaResourceName"": ""{routeName}"",
            ""HttpMethod"": ""POST"",
            ""Path"": ""/{routeName}""
        }}");

        var settings = new RunCommandSettings
        {
            LambdaEmulatorPort = lambdaPort,
            NoLaunchWindow = true,
            ApiGatewayEmulatorMode = apiGatewayMode,
            ApiGatewayEmulatorPort = apiGatewayPort
        };

        var command = new RunCommand(MockInteractiveService.Object, MockEnvironmentManager.Object);
        var context = new CommandContext(new List<string>(), MockRemainingArgs.Object, "run", null);
        _ = command.ExecuteAsync(context, settings, cancellationTokenSource);

        await Task.Delay(2000, cancellationTokenSource.Token);
    }
} 
