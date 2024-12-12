using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;

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

    /// <summary>
    /// Creates the Web API and runs it in the background.
    /// </summary>
    public static ApiGatewayEmulatorProcess Startup(RunCommandSettings settings, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddApiGatewayEmulatorServices();
        
        var serviceUrl = $"http://{settings.Host}:{settings.ApiGatewayEmulatorPort}";
        builder.WebHost.UseUrls(serviceUrl);
        builder.WebHost.SuppressStatusMessages(true);

        builder.Services.AddHealthChecks();

        var app = builder.Build();

        app.UseHttpsRedirection();

        app.MapHealthChecks("/__lambda_test_tool_apigateway_health__");

        app.Map("/{**catchAll}", (HttpContext context, IApiGatewayRouteConfigService routeConfigService) =>
        {
            var routeConfig = routeConfigService.GetRouteConfig(context.Request.Method, context.Request.Path);
            if (routeConfig == null)
            {
                app.Logger.LogInformation("Unable to find a configured Lambda route for the specified method and path: {Method} {Path}", 
                    context.Request.Method, context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.Headers.Append("x-amzn-errortype", "MissingAuthenticationTokenException");
                return Results.Json(new { message = "Missing Authentication Token" });
            }
            
            if (settings.ApiGatewayEmulatorMode.Equals(ApiGatewayEmulatorMode.HttpV2))
            {
                // TODO: Translate to APIGatewayHttpApiV2ProxyRequest
            }
            else
            {
                // TODO: Translate to APIGatewayProxyRequest
            }

            return Results.Ok();
        });

        var runTask = app.RunAsync(cancellationToken);
        
        return new ApiGatewayEmulatorProcess
        {
            Services = app.Services,
            RunningTask = runTask,
            ServiceUrl = serviceUrl
        };
    }
}