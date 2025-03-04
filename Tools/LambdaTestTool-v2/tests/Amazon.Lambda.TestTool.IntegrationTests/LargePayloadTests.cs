// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Xunit;
using Xunit.Abstractions;
using Amazon.Lambda.TestTool.Models;
using System.Net;

namespace Amazon.Lambda.TestTool.IntegrationTests;

[Collection("ApiGatewayTests")]
public class LargePayloadTests : BaseApiGatewayTest
{
    public LargePayloadTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Theory]
    [InlineData(ApiGatewayEmulatorMode.Rest)]
    [InlineData(ApiGatewayEmulatorMode.HttpV1)]
    public async Task TestLambdaWithLargeRequestPayload_RestAndV1(ApiGatewayEmulatorMode mode)
    {
        var ports = await GetFreePorts();
        var lambdaPort = ports.lambdaPort;
        var apiGatewayPort = ports.apiGatewayPort;
        
        CancellationTokenSource = new CancellationTokenSource();
        CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(120));
        var consoleError = Console.Error;
        
        try
        {
            Console.SetError(TextWriter.Null);
            await StartTestToolProcessAsync(mode, "largerequestfunction", lambdaPort, apiGatewayPort, CancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayProxyRequest request, ILambdaContext context) =>
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = request.Body.Length.ToString()
                };
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/largerequestfunction")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            // Create a payload just over 6MB
            var largePayload = new string('X', 6 * 1024 * 1024 + 1024); // 6MB + 1KB

            var response = await TestEndpoint("largerequestfunction", apiGatewayPort, payload: largePayload);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains(mode == ApiGatewayEmulatorMode.Rest ? "Request Too Long" : "Request Entity Too Large", responseContent);
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            Console.SetError(consoleError);
        }
    }

    [Fact]
    public async Task TestLambdaWithLargeRequestPayload_HttpV2()
    {
        var ports = await GetFreePorts();
        var lambdaPort = ports.lambdaPort;
        var apiGatewayPort = ports.apiGatewayPort;
        
        CancellationTokenSource = new CancellationTokenSource();
        CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(120));
        var consoleError = Console.Error;
        
        try
        {
            Console.SetError(TextWriter.Null);
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV2, "largerequestfunction", lambdaPort, apiGatewayPort, CancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = request.Body.Length.ToString()
                };
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/largerequestfunction")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            // Create a payload just over 6MB
            var largePayload = new string('X', 6 * 1024 * 1024 + 1024); // 6MB + 1KB

            var response = await TestEndpoint("largerequestfunction", apiGatewayPort, payload: largePayload);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("Request Entity Too Large", responseContent);
        }
        finally
        {
            await CancellationTokenSource.CancelAsync();
            Console.SetError(consoleError);
        }
    }
} 
