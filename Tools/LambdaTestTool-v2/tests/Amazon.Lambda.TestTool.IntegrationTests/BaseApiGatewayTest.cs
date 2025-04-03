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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Amazon.Lambda.RuntimeSupport;
using Castle.DynamicProxy;

namespace Amazon.Lambda.TestTool.IntegrationTests;

public abstract class BaseApiGatewayTest
{
    protected readonly ITestOutputHelper TestOutputHelper;
    protected readonly Mock<IEnvironmentManager> MockEnvironmentManager;
    protected readonly Mock<IToolInteractiveService> MockInteractiveService;
    protected readonly Mock<IRemainingArguments> MockRemainingArgs;
    protected CancellationTokenSource CancellationTokenSource;
    protected static readonly object TestLock = new();
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly IConfiguration Configuration;

    protected BaseApiGatewayTest(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;

        Environment.SetEnvironmentVariable("LAMBDA_RUNTIMESUPPORT_DEBUG", "1");

        MockEnvironmentManager = new Mock<IEnvironmentManager>();
        MockInteractiveService = new Mock<IToolInteractiveService>();
        MockRemainingArgs = new Mock<IRemainingArguments>();
        CancellationTokenSource = new CancellationTokenSource();

        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder =>
        {
            builder
                .AddConfiguration(Configuration.GetSection("Logging"))
                .AddConsole()
                .AddDebug()
                .AddXunit(testOutputHelper); // For test output
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        // Configure AWS options
        AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
        AWSConfigs.LoggingConfig.LogMetrics = true;
        AWSConfigs.LoggingConfig.LogResponses = ResponseLoggingOption.Always;
        AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.JSON;
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
            ""Endpoint"": ""http://127.0.0.1:{lambdaPort}"",
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
            var healthUrl = $"http://127.0.0.1:{apiGatewayPort}/__lambda_test_tool_apigateway_health__";

            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    var response = await client.GetAsync(healthUrl);
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
        TestOutputHelper.WriteLine($"Testing endpoint: http://127.0.0.1:{apiGatewayPort}/{routeName}");
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
                            $"http://127.0.0.1:{apiGatewayPort}/{routeName}",
                            new StringContent(payload ?? "hello world", Encoding.UTF8, new MediaTypeHeaderValue("text/plain"))),
                        "GET" => await client.GetAsync($"http://127.0.0.1:{apiGatewayPort}/{routeName}"),
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
        var random = new Random();
        var port = random.Next(49152, 65535);
        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start();
            return port;
        }
        catch (SocketException)
        {
            return GetFreePort();
        }
        finally
        {
            listener.Stop();
        }
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
