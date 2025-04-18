// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Processes;
using Amazon.Lambda.TestTool.Tests.Common;
using Amazon.Lambda.TestTool.Tests.Common.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.TestTool.UnitTests.Processes;

public class ApiGatewayEmulatorProcessTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [InlineData(ApiGatewayEmulatorMode.Rest, HttpStatusCode.Forbidden, "{\"message\":\"Missing Authentication Token\"}")]
    [InlineData(ApiGatewayEmulatorMode.HttpV1, HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}")]
    [InlineData(ApiGatewayEmulatorMode.HttpV2, HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}")]
    public async Task RouteNotFound(ApiGatewayEmulatorMode mode, HttpStatusCode statusCode, string body)
    {
        // Arrange
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var gatewayPort = TestHelpers.GetNextApiGatewayPort();
        var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(5000);
        var settings = new RunCommandSettings { LambdaEmulatorPort = lambdaPort, ApiGatewayEmulatorPort = gatewayPort,  ApiGatewayEmulatorMode = mode, NoLaunchWindow = true};
        var apiUrl = $"http://{settings.LambdaEmulatorHost}:{settings.ApiGatewayEmulatorPort}/__lambda_test_tool_apigateway_health__";

        // Act
        var process = ApiGatewayEmulatorProcess.Startup(settings, cancellationSource.Token);
        var isApiRunning = await TestHelpers.WaitForApiToStartAsync(apiUrl);
        var response = await TestHelpers.SendRequest($"{process.ServiceUrl}/invalid");
        await cancellationSource.CancelAsync();

        // Assert
        await process.RunningTask;
        Assert.True(isApiRunning);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
    }
}
