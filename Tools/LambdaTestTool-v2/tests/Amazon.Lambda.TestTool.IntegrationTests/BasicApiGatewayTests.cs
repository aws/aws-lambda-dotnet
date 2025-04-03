// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Tests.Common.Retries;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.TestTool.IntegrationTests;

[Collection("ApiGatewayTests")]
public class BasicApiGatewayTests : BaseApiGatewayTest
{
    public BasicApiGatewayTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [RetryFact]
    public async Task TestLambdaToUpperV2()
    {
        var ports = GetFreePorts();
        var lambdaPort = ports.lambdaPort;
        var apiGatewayPort = ports.apiGatewayPort;
        
        CancellationTokenSource = new CancellationTokenSource();
        CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(120));
        var consoleError = Console.Error;
        
        try
        {
            Console.SetError(TextWriter.Null);
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV2, "testfunction", lambdaPort, apiGatewayPort, CancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"TestLambdaToUpperV2");
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = request.Body.ToUpper()
                };
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"127.0.0.1:{lambdaPort}/testfunction")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            var response = await TestEndpoint("testfunction", apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            Console.SetError(consoleError);
        }
    }

    [RetryFact]
    public async Task TestLambdaToUpperRest()
    {
        var ports = GetFreePorts();
        var lambdaPort = ports.lambdaPort;
        var apiGatewayPort = ports.apiGatewayPort;
        
        CancellationTokenSource = new CancellationTokenSource();
        CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(120));
        var consoleError = Console.Error;
        
        try
        {
            Console.SetError(TextWriter.Null);
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.Rest, "testfunction", lambdaPort, apiGatewayPort, CancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayProxyRequest request, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"TestLambdaToUpperRest");
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = request.Body.ToUpper()
                };
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"127.0.0.1:{lambdaPort}/testfunction")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            var response = await TestEndpoint("testfunction", apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            Console.SetError(consoleError);
        }
    }

    [RetryFact]
    public async Task TestLambdaToUpperV1()
    {
        var ports = GetFreePorts();
        var lambdaPort = ports.lambdaPort;
        var apiGatewayPort = ports.apiGatewayPort;
        
        CancellationTokenSource = new CancellationTokenSource();
        CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(120));
        var consoleError = Console.Error;
        
        try
        {
            Console.SetError(TextWriter.Null);
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV1, "testfunction", lambdaPort, apiGatewayPort, CancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayProxyRequest request, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"TestLambdaToUpperV1");
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = request.Body.ToUpper()
                };
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"127.0.0.1:{lambdaPort}/testfunction")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            var response = await TestEndpoint("testfunction", apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            Console.SetError(consoleError);
        }
    }
} 
