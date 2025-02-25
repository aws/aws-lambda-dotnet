// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Mvc;

namespace Amazon.Lambda.TestTool.Services;

public class LambdaRuntimeApi
{
    internal const string DefaultFunctionName = "__DefaultFunction__";
    private const string HeaderBreak = "-----------------------------------";
    private const int MaxRequestSize = 6 * 1024 * 1024;
    private const int MaxResponseSize = 6 * 1024 * 1024;

    private readonly IRuntimeApiDataStoreManager _runtimeApiDataStoreManager;

    internal LambdaRuntimeApi(WebApplication app)
    {
        _runtimeApiDataStoreManager = app.Services.GetRequiredService<IRuntimeApiDataStoreManager>();

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

    public static void SetupLambdaRuntimeApiEndpoints(WebApplication app)
    {
        _ = new LambdaRuntimeApi(app);
    }

    public Task PostEventDefaultFunction(HttpContext ctx)
    {
        return PostEvent(ctx, DefaultFunctionName);
    }

    public async Task PostEvent(HttpContext ctx, string functionName)
    {
        var runtimeDataStore = _runtimeApiDataStoreManager.GetLambdaRuntimeDataStore(functionName);

        // RequestResponse mode is the default when invoking with the Lambda service client.
        var isRequestResponseMode = true;
        if (ctx.Request.Headers.TryGetValue("X-Amz-Invocation-Type", out var invocationType))
        {
            isRequestResponseMode = string.Equals(invocationType, "RequestResponse", StringComparison.InvariantCulture);
        }

        using var reader = new StreamReader(ctx.Request.Body);
        var testEvent = await reader.ReadToEndAsync();

        if (Encoding.UTF8.GetByteCount(testEvent) > MaxRequestSize)
        {
            ctx.Response.StatusCode = 413;
            ctx.Response.Headers.ContentType = "application/json";
            ctx.Response.Headers["X-Amzn-Errortype"] = Exceptions.RequestEntityTooLargeException;
            var errorData = Encoding.UTF8.GetBytes($"Request must be smaller than {MaxRequestSize} bytes for the InvokeFunction operation");
            ctx.Response.Headers.ContentLength = errorData.Length;
            await ctx.Response.Body.WriteAsync(errorData);
            return;
        }

        var evnt = runtimeDataStore.QueueEvent(testEvent, isRequestResponseMode);

        if (isRequestResponseMode)
        {
            evnt.WaitForCompletion();

            if (evnt.EventStatus == EventContainer.Status.Success)
            {
                var result = Results.Ok(evnt.Response);
                ctx.Response.StatusCode = 200;

                if (!string.IsNullOrEmpty(evnt.Response))
                {
                    var responseData = Encoding.UTF8.GetBytes(evnt.Response);
                    ctx.Response.Headers.ContentType = "application/json";
                    ctx.Response.Headers.ContentLength = responseData.Length;
                    await ctx.Response.Body.WriteAsync(responseData);
                }
            }
            else
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.Headers["X-Amz-Function-Error"] = evnt.ErrorType;
                if (!string.IsNullOrEmpty(evnt.ErrorResponse))
                {
                    var errorData = Encoding.UTF8.GetBytes(evnt.ErrorResponse);
                    ctx.Response.Headers.ContentType = "application/json";
                    ctx.Response.Headers.ContentLength = errorData.Length;
                    await ctx.Response.Body.WriteAsync(errorData);
                }
            }
            evnt.Dispose();
        }
        else
        {
            ctx.Response.StatusCode = 202;
        }
    }

    public Task GetNextInvocationDefaultFunction(HttpContext ctx)
    {
        return GetNextInvocation(ctx, DefaultFunctionName);
    }

    public async Task GetNextInvocation(HttpContext ctx, string functionName)
    {
        var runtimeDataStore = _runtimeApiDataStoreManager.GetLambdaRuntimeDataStore(functionName);

        EventContainer? activeEvent;

        // A Lambda function should never call to get the next event till it was done
        // processing the active event and there is no more active event. If there
        // is an active event still executing that most likely means the previous debug session was
        // killed leaving the event active. In that case resend the active event
        // to restart debugging the event.
        if (runtimeDataStore.ActiveEvent != null && runtimeDataStore.ActiveEvent.EventStatus == EventContainer.Status.Executing)
        {
            activeEvent = runtimeDataStore.ActiveEvent;
        }
        else
        {
            while (!runtimeDataStore.TryActivateEvent(out activeEvent))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
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
        var runtimeDataStore = _runtimeApiDataStoreManager.GetLambdaRuntimeDataStore(functionName);

        using var reader = new StreamReader(ctx.Request.Body);
        var response = await reader.ReadToEndAsync();

        if (Encoding.UTF8.GetByteCount(response) > MaxResponseSize)
        {
            runtimeDataStore.ReportError(awsRequestId, "ResponseSizeTooLarge", $"Response payload size exceeded maximum allowed payload size ({MaxResponseSize} bytes)");

            Console.WriteLine(HeaderBreak);
            Console.WriteLine($"Response for request {awsRequestId}");
            Console.WriteLine(response);

            return Results.Accepted(null, new StatusResponse { Status = "success" });
        }

        runtimeDataStore.ReportSuccess(awsRequestId, response);

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
        var runtimeDataStore = _runtimeApiDataStoreManager.GetLambdaRuntimeDataStore(functionName);

        using var reader = new StreamReader(ctx.Request.Body);
        var errorBody = await reader.ReadToEndAsync();

        runtimeDataStore.ReportError(awsRequestId, errorType, errorBody);
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
