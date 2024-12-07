using System.Text;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Mvc;

// TODO:
// Make IRuntimeApiDataStore separate the events per function name
// When PostEvent being syncronous when X-Amz-Invocation-Type header is not set or set to RequestResponse.
//          https://docs.aws.amazon.com/lambda/latest/api/API_Invoke.html#lambda-Invoke-request-InvocationType


namespace Amazon.Lambda.TestTool.Services;

public class LambdaRuntimeApi
{
    private const string DefaultFunctionName = "__DefaultFunction__";
    private const string HeaderBreak = "-----------------------------------";

    private readonly IRuntimeApiDataStore _runtimeApiDataStore;

    public LambdaRuntimeApi(WebApplication app, IRuntimeApiDataStore runtimeApiDataStore)
    {
        _runtimeApiDataStore = runtimeApiDataStore;
        app.MapPost("/2015-03-31/functions/function/invocations", (Delegate)PostEventDefaultFunction);
        app.MapPost("/2015-03-31/functions/{functionName}/invocations", PostEvent);

        app.MapGet("/2018-06-01/runtime/invocation/next", GetNextInvocationDefaultFunction);
        app.MapGet("/{functionName}/2018-06-01/runtime/invocation/next", GetNextInvocation);

        app.MapPost("/2018-06-01/runtime/init/error", (Delegate)PostInitErrorDefaultFunction);
        app.MapPost("/{functionName}/2018-06-01/runtime/init/error", PostInitError);

        app.MapPost("/2018-06-01/runtime/invocation/{awsRequestId}/response", (Delegate)PostInvocationResponseDefaultFunction);
        app.MapPost("/{functionName}/2018-06-01/runtime/invocation/{awsRequestId}/response", PostInvocationResponse);

        app.MapPost("/2018-06-01/runtime/invocation/{awsRequestId}/error", (Delegate)PostErrorDefaultFunction);
        app.MapPost("/{functionName}/2018-06-01/runtime/invocation/{awsRequestId}/error", PostError);
    }

    public Task<IResult> PostEventDefaultFunction(HttpContext ctx)
    {
        return PostEvent(ctx, DefaultFunctionName);
    }

    public async Task<IResult> PostEvent(HttpContext ctx, string functionName)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var testEvent = await reader.ReadToEndAsync();
        _runtimeApiDataStore.QueueEvent(testEvent);

        return Results.Accepted();
    }

    public Task GetNextInvocationDefaultFunction(HttpContext ctx)
    {
        return GetNextInvocation(ctx, DefaultFunctionName);
    }

    public async Task GetNextInvocation(HttpContext ctx, string functionName)
    {
        EventContainer? activeEvent;
        while (!_runtimeApiDataStore.TryActivateEvent(out activeEvent))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        if (activeEvent == null)
            return;

        Console.WriteLine(HeaderBreak);
        Console.WriteLine($"Next invocation returned: {activeEvent.AwsRequestId}");

        ctx.Response.Headers["Lambda-Runtime-Aws-Request-Id"] = activeEvent.AwsRequestId;
        ctx.Response.Headers["Lambda-Runtime-Trace-Id"] = Guid.NewGuid().ToString();
        ctx.Response.Headers["Lambda-Runtime-Invoked-Function-Arn"] = activeEvent.FunctionArn;
        ctx.Response.StatusCode = 200;

        if (activeEvent != null && activeEvent.EventJson.Length != 0)
        {
            // The event is written directly to the response stream to avoid ASP.NET Core attempting any
            // encoding on content passed in the Ok() method.
            ctx.Response.Headers["Content-Type"] = "application/json";
            var buffer = UTF8Encoding.UTF8.GetBytes(activeEvent.EventJson);
            await ctx.Response.Body.WriteAsync(buffer, 0, buffer.Length);
            ctx.Response.Body.Close();
        }
    }

    public IResult PostInitErrorDefaultFunction([FromHeader(Name = "Lambda-Runtime-Function-Error-Type")] string errorType, [FromBody] string error)
    {
        return PostInitError(DefaultFunctionName, errorType, error);
    }

    public IResult PostInitError(string functionName, [FromHeader(Name = "Lambda-Runtime-Function-Error-Type")] string errorType, [FromBody] string error)
    {
        Console.Error.WriteLine("Init Error Type: " + errorType);
        Console.Error.WriteLine(error);
        Console.Error.WriteLine(HeaderBreak);
        return Results.Accepted(null, new StatusResponse { Status = "success" });
    }


    public Task<IResult> PostInvocationResponseDefaultFunction(HttpContext ctx, string awsRequestId)
    {
        return PostInvocationResponse(ctx, DefaultFunctionName, awsRequestId);
    }

    public async Task<IResult> PostInvocationResponse(HttpContext ctx, string functionName, string awsRequestId)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var response = await reader.ReadToEndAsync();

        _runtimeApiDataStore.ReportSuccess(awsRequestId, response);

        Console.WriteLine(HeaderBreak);
        Console.WriteLine($"Response for request {awsRequestId}");
        Console.WriteLine(response);

        return Results.Accepted(null, new StatusResponse { Status = "success" });
    }

    public Task<IResult> PostErrorDefaultFunction(HttpContext ctx, string awsRequestId, [FromHeader(Name = "Lambda-Runtime-Function-Error-Type")] string errorType)
    {
        return PostError(ctx, DefaultFunctionName, awsRequestId, errorType);
    }

    public async Task<IResult> PostError(HttpContext ctx, string functionName, string awsRequestId, [FromHeader(Name = "Lambda-Runtime-Function-Error-Type")] string errorType)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var errorBody = await reader.ReadToEndAsync();

        _runtimeApiDataStore.ReportError(awsRequestId, errorType, errorBody);
        await Console.Error.WriteLineAsync(HeaderBreak);
        await Console.Error.WriteLineAsync($"Request {awsRequestId} Error Type: {errorType}");
        await Console.Error.WriteLineAsync(errorBody);

        return Results.Accepted(null, new StatusResponse { Status = "success" });
    }
}

internal class StatusResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public required string Status { get; init; }
}