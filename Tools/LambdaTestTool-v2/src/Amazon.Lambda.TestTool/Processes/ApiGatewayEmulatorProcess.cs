// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;

using System.Text.Json;
using Amazon.Lambda.TestTool.Configuration;
using Amazon.Lambda.TestTool.Utilities;

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

        Utils.ConfigureWebApplicationBuilder(builder);

        builder.Services.AddApiGatewayEmulatorServices();

        var serviceUrl = $"http://{settings.LambdaEmulatorHost}:{settings.ApiGatewayEmulatorPort}";
        builder.WebHost.UseUrls(serviceUrl);
        builder.WebHost.SuppressStatusMessages(true);

        builder.Services.AddHealthChecks();

        var app = builder.Build();

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

            using var lambdaClient = CreateLambdaServiceClient(routeConfig, settings);
            var response =  await lambdaClient.InvokeAsync(invokeRequest);

            if (response.FunctionError == null) // response is successful
            {
                if (settings.ApiGatewayEmulatorMode.Equals(ApiGatewayEmulatorMode.HttpV2))
                {
                    var lambdaResponse = response.ToApiGatewayHttpApiV2ProxyResponse();
                    await lambdaResponse.ToHttpResponseAsync(context);
                }
                else
                {
                    var lambdaResponse = response.ToApiGatewayProxyResponse(settings.ApiGatewayEmulatorMode.Value);
                    await lambdaResponse.ToHttpResponseAsync(context, settings.ApiGatewayEmulatorMode.Value);
                }
            }
            else
            {
                // For function errors, api gateway just displays them as an internal server error, so we convert them to the correct error response here.

                if (settings.ApiGatewayEmulatorMode.Equals(ApiGatewayEmulatorMode.HttpV2))
                {
                    var lambdaResponse = InvokeResponseExtensions.ToHttpApiV2ErrorResponse();
                    await lambdaResponse.ToHttpResponseAsync(context);
                }
                else
                {
                    var lambdaResponse = InvokeResponseExtensions.ToApiGatewayErrorResponse(settings.ApiGatewayEmulatorMode.Value);
                    await lambdaResponse.ToHttpResponseAsync(context, settings.ApiGatewayEmulatorMode.Value);
                }
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

    /// <summary>
    /// Creates an Amazon Lambda service client with the specified configuration.
    /// </summary>
    /// <param name="routeConfig">The API Gateway route configuration containing the endpoint information.
    /// If the endpoint is specified in routeConfig, it will be used as the service URL.</param>
    /// <param name="settings">The run command settings containing host and port information.
    /// If routeConfig endpoint is null, the service URL will be constructed using settings.Host and settings.Port.</param>
    /// <returns>An instance of IAmazonLambda configured with the specified endpoint and credentials.</returns>
    /// <remarks>
    /// The function uses hard-coded AWS credentials ("accessKey", "secretKey") for authentication since they are not actually being used.
    /// The service URL is determined by either:
    /// - Using routeConfig.Endpoint if it's not null
    /// - Combining settings.Host and settings.Port if routeConfig.Endpoint is null
    /// </remarks>
    private static IAmazonLambda CreateLambdaServiceClient(ApiGatewayRouteConfig routeConfig, RunCommandSettings settings)
    {
        var endpoint = routeConfig.Endpoint ?? $"http://{settings.LambdaEmulatorHost}:{settings.LambdaEmulatorPort}";

        var lambdaConfig = new AmazonLambdaConfig
        {
            ServiceURL = endpoint
        };

        return new AmazonLambdaClient(new Amazon.Runtime.BasicAWSCredentials("accessKey", "secretKey"), lambdaConfig);
    }
}
