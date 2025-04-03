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
public class EdgeCaseTests : BaseApiGatewayTest
{
    public EdgeCaseTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [RetryFact]
    public async Task TestLambdaReturnString()
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
            await StartTestToolProcessAsync(ApiGatewayEmulatorMode.HttpV2, "stringfunction", lambdaPort, apiGatewayPort, CancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"TestLambdaReturnString");
                return request.Body.ToUpper();
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/stringfunction")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            var response = await TestEndpoint("stringfunction", apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            _ = CancellationTokenSource.CancelAsync();
            Console.SetError(consoleError);
        }
    }

    [RetryFact]
    public async Task TestLambdaWithNullEndpoint()
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
            await StartTestToolProcessWithNullEndpoint(ApiGatewayEmulatorMode.HttpV2, "testfunction", lambdaPort, apiGatewayPort, CancellationTokenSource);
            await WaitForGatewayHealthCheck(apiGatewayPort);

            var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
            {
                TestOutputHelper.WriteLine($"TestLambdaWithNullEndpoint");
                return new APIGatewayHttpApiV2ProxyResponse
                {
                    StatusCode = 200,
                    Body = request.Body.ToUpper()
                };
            };

            _ = LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                .ConfigureOptions(x => x.RuntimeApiEndpoint = $"localhost:{lambdaPort}/testfunction")
                .Build()
                .RunAsync(CancellationTokenSource.Token);

            var response = await TestEndpoint("testfunction", apiGatewayPort);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("HELLO WORLD", responseContent);
        }
        finally
        {
            _ = CancellationTokenSource.CancelAsync();
            Console.SetError(consoleError);
        }
    }
} 
