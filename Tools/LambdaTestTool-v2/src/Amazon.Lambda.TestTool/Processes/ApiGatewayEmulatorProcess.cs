// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;

using System.Text.Json;

namespace Amazon.Lambda.TestTool.Processes;

/// <summary>
/// A process that runs the API Gateway emulator.
/// </summary>
public class ApiGatewayEmulatorProcess
{
    /// <summary>
    /// The service provider that will contain all the registered services.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// The API Gateway emulator task that was started.
    /// </summary>
    public required Task RunningTask { get; init; }

    /// <summary>
    /// The endpoint of the API Gateway emulator.
    /// </summary>
    public required string ServiceUrl { get; init; }

    private static readonly JsonSerializerOptions _jsonSerializationOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Creates the Web API and runs it in the background.
    /// </summary>
    public static ApiGatewayEmulatorProcess Startup(RunCommandSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.ApiGatewayEmulatorMode is null)
        {
            throw new InvalidApiGatewayModeException("The API Gateway emulator mode was not provided.");
        }

        var builder = WebApplication.CreateBuilder();

        builder.Services.AddApiGatewayEmulatorServices();

        var serviceUrl = $"http://{settings.Host}:{settings.ApiGatewayEmulatorPort}";
        builder.WebHost.UseUrls(serviceUrl);
        builder.WebHost.SuppressStatusMessages(true);

        builder.Services.AddHealthChecks();

        var app = builder.Build();

        app.UseHttpsRedirection();

        app.MapHealthChecks("/__lambda_test_tool_apigateway_health__");

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            app.Logger.LogInformation("The API Gateway Emulator is available at: {ServiceUrl}", serviceUrl);
        });

        app.Map("/{**catchAll}", async (HttpContext context, IApiGatewayRouteConfigService routeConfigService) =>
        {
            var routeConfig = routeConfigService.GetRouteConfig(context.Request.Method, context.Request.Path);
            if (routeConfig == null)
            {
                app.Logger.LogInformation("Unable to find a configured Lambda route for the specified method and path: {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                await ApiGatewayResults.RouteNotFoundAsync(context, (ApiGatewayEmulatorMode)settings.ApiGatewayEmulatorMode);
                return;
            }

            // Convert ASP.NET Core request to API Gateway event object
            var lambdaRequestStream = new MemoryStream();
            if (settings.ApiGatewayEmulatorMode.Equals(ApiGatewayEmulatorMode.HttpV2))
            {
                var lambdaRequest = await context.ToApiGatewayHttpV2Request(routeConfig);
                JsonSerializer.Serialize<APIGatewayHttpApiV2ProxyRequest>(lambdaRequestStream, lambdaRequest, _jsonSerializationOptions);
            }
            else
            {
                var lambdaRequest = await context.ToApiGatewayRequest(routeConfig, settings.ApiGatewayEmulatorMode.Value);
                JsonSerializer.Serialize<APIGatewayProxyRequest>(lambdaRequestStream, lambdaRequest, _jsonSerializationOptions);
            }
            lambdaRequestStream.Position = 0;

            // Invoke Lamdba function via the test tool's Lambda Runtime API.
            var invokeRequest = new InvokeRequest
            {
                FunctionName = routeConfig.LambdaResourceName,
                InvocationType = InvocationType.RequestResponse,
                PayloadStream = lambdaRequestStream                
            };

            using var lambdaClient = CreateLambdaServiceClient(routeConfig);
            var response =  await lambdaClient.InvokeAsync(invokeRequest);

            if (response.FunctionError != null)
            {
                // TODO: Mimic API Gateway's behavior when Lambda function has an exception during invocation.
                context.Response.StatusCode = 500;
                return;
            }

            // Convert API Gateway response object returned from Lambda to ASP.NET Core response.
            if (settings.ApiGatewayEmulatorMode.Equals(ApiGatewayEmulatorMode.HttpV2))
            {
                var lambdaResponse = response.ToApiGatewayHttpApiV2ProxyResponse();
                await lambdaResponse.ToHttpResponseAsync(context);
                return;
            }
            else
            {
                var lambdaResponse = response.ToApiGatewayProxyResponse(settings.ApiGatewayEmulatorMode.Value);
                await lambdaResponse.ToHttpResponseAsync(context, settings.ApiGatewayEmulatorMode.Value);
                return;
            }
        });

        var runTask = app.RunAsync(cancellationToken);

        return new ApiGatewayEmulatorProcess
        {
            Services = app.Services,
            RunningTask = runTask,
            ServiceUrl = serviceUrl
        };
    }
    
    private static IAmazonLambda CreateLambdaServiceClient(ApiGatewayRouteConfig routeConfig)
    {
        // TODO: Handle routeConfig.Endpoint to null and use the settings versions of runtime.
        var lambdaConfig = new AmazonLambdaConfig
        {
            ServiceURL = routeConfig.Endpoint
        };

        return new AmazonLambdaClient(new Amazon.Runtime.BasicAWSCredentials("accessKey", "secretKey"), lambdaConfig);
    }
}
