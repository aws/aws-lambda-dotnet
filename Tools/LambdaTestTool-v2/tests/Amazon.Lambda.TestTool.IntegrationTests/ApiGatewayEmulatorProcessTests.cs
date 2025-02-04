// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestTool.Commands;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.IO;
using Moq;
using Spectre.Console.Cli;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Amazon.Lambda.TestTool.IntegrationTests;

public class ApiGatewayEmulatorProcessTests : IAsyncDisposable
{
    private readonly Mock<IEnvironmentManager> _mockEnvironmentManager = new();
    private readonly Mock<IToolInteractiveService> _mockInteractiveService = new();
    private readonly Mock<IRemainingArguments> _mockRemainingArgs = new();
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ConcurrentQueue<string> _logMessages = new();
    private readonly SemaphoreSlim _logSemaphore = new(1, 1);

    public ApiGatewayEmulatorProcessTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestLambdaToUpperV2()
    {
        var (lambdaPort, apiGatewayPort) = await GetFreePorts();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV2, "testfunction", lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = request.Body.ToUpper()
                };
            };

            Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"localhost:{lambdaPort}/testfunction");
            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                    .Build()
                    .RunAsync(cancellationTokenSource.Token);

            var response = await TestEndpoint("testfunction", apiGatewayPort);
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
        var (lambdaPort, apiGatewayPort) = await GetFreePorts();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.Rest, "testfunction", lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayProxyRequest request, ILambdaContext context) =>
            {
                return new APIGatewayProxyResponse()
                {
                    StatusCode = 200,
                    Body = request.Body.ToUpper(),
                    IsBase64Encoded = false,
                };
            };

            Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"localhost:{lambdaPort}/testfunction");
            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                    .Build()
                    .RunAsync(cancellationTokenSource.Token);

            var response = await TestEndpoint("testfunction", apiGatewayPort);
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
        var (lambdaPort, apiGatewayPort) = await GetFreePorts();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV1, "testfunction", lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayProxyRequest request, ILambdaContext context) =>
            {
                return new APIGatewayProxyResponse()
                {
                    StatusCode = 200,
                    Body = request.Body.ToUpper(),
                    IsBase64Encoded = false,
                };
            };

            Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"localhost:{lambdaPort}/testfunction");
            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                    .Build()
                    .RunAsync(cancellationTokenSource.Token);

            var response = await TestEndpoint("testfunction", apiGatewayPort);
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
        var (lambdaPort, apiGatewayPort) = await GetFreePorts();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV2, "binaryfunction", lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                // Create a simple binary pattern (for example, counting bytes from 0 to 255)
                byte[] binaryData = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    binaryData[i] = (byte)i;
                }

                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = Convert.ToBase64String(binaryData),
                    IsBase64Encoded = true,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/octet-stream" }
                    }
                };
            };

            Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"localhost:{lambdaPort}/binaryfunction");
            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                    .Build()
                    .RunAsync(cancellationTokenSource.Token);

            var response = await TestEndpoint("binaryfunction", apiGatewayPort, httpMethod: "POST");

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
        var (lambdaPort, apiGatewayPort) = await GetFreePorts();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV2, "stringfunction", lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                return request.Body.ToUpper();
            };

            Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"localhost:{lambdaPort}/stringfunction");
            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                    .Build()
                    .RunAsync(cancellationTokenSource.Token);

            var response = await TestEndpoint("stringfunction", apiGatewayPort);
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
        var (lambdaPort, apiGatewayPort) = await GetFreePorts();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            StartTestToolProcessWithNullEndpoint(ApiGatewayEmulatorMode.HttpV2, "testfunction", lambdaPort, apiGatewayPort, cancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = request.Body.ToUpper()
                };
            };

            Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"localhost:{lambdaPort}/testfunction");
            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                    .Build()
                    .RunAsync(cancellationTokenSource.Token);

            var response = await TestEndpoint("testfunction", apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    private async Task<HttpResponseMessage> TestEndpoint(string routeName, int apiGatewayPort, string httpMethod = "POST")
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(30);
        Exception? lastException = null;

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                return httpMethod.ToUpper() switch
                {
                    "POST" => await client.PostAsync(
                        $"http://localhost:{apiGatewayPort}/{routeName}",
                        new StringContent("hello world", Encoding.UTF8, "text/plain")),
                    "GET" => await client.GetAsync($"http://localhost:{apiGatewayPort}/{routeName}"),
                    _ => throw new ArgumentException($"Unsupported HTTP method: {httpMethod}")
                };
            }
            catch (Exception ex)
            {
                lastException = ex;
                await LogMessage($"Request attempt failed: {ex.Message}");
                await Task.Delay(500);
            }
        }

        throw new TimeoutException($"Failed to complete request within timeout period: {lastException?.Message}", lastException);
    }


    private async Task StartTestToolProcessAsync(ApiGatewayEmulatorMode apiGatewayMode, string routeName, int lambdaPort, int apiGatewayPort, CancellationTokenSource cancellationTokenSource, string httpMethod = "POST")
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

        var command = new RunCommand(_mockInteractiveService.Object, _mockEnvironmentManager.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        _ = command.ExecuteAsync(context, settings, cancellationTokenSource);

        // Give the process time to start
        await Task.Delay(2000, cancellationTokenSource.Token);
    }


    private void StartTestToolProcessWithNullEndpoint(ApiGatewayEmulatorMode apiGatewayMode, string routeName, int lambdaPort, int apiGatewayPort, CancellationTokenSource cancellationTokenSource)
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

        var command = new RunCommand(_mockInteractiveService.Object, _mockEnvironmentManager.Object);
        var context = new CommandContext(new List<string>(), _mockRemainingArgs.Object, "run", null);
        _ = command.ExecuteAsync(context, settings, cancellationTokenSource);
    }

    private async Task<(int lambdaPort, int apiGatewayPort)> GetFreePorts()
    {
        var lambdaPort = GetFreePort();
        int apiGatewayPort;
        do
        {
            apiGatewayPort = GetFreePort();
        } while (apiGatewayPort == lambdaPort);

        return (lambdaPort, apiGatewayPort);
    }

    private int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task WaitForGatewayHealthCheck(int apiGatewayPort)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(30);
        var healthUrl = $"http://localhost:{apiGatewayPort}/__lambda_test_tool_apigateway_health__";

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                var response = await client.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    await LogMessage("API Gateway health check succeeded");
                    // Add additional delay after successful health check
                    await Task.Delay(1000);
                    return;
                }
            }
            catch (Exception ex)
            {
                await LogMessage($"Health check attempt failed: {ex.Message}");
                await Task.Delay(500);
            }
        }
        throw new TimeoutException("API Gateway failed to start within timeout period");
    }


    private async Task LogMessage(string message)
    {
        await _logSemaphore.WaitAsync();
        try
        {
            Console.WriteLine(message);
            _logMessages.Enqueue(message);
        }
        finally
        {
            _logSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        while (_logMessages.TryDequeue(out var message))
        {
            _testOutputHelper.WriteLine(message);
        }
        _logSemaphore.Dispose();
    }
}
