// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;

namespace Amazon.Lambda.TestTool.Services.DurableExecution;

/// <summary>
/// Emulates the AWS Lambda durable-execution data plane as HTTP endpoints hosted alongside the
/// Lambda Runtime API on the same web app. A durable function whose <c>IAmazonLambda</c> client
/// is redirected here (via <c>AWS_ENDPOINT_URL_LAMBDA</c>) checkpoints and reads execution state
/// against these routes instead of the real service.
/// </summary>
/// <remarks>
/// Routes mirror <c>lambda-2015-03-31.normal.json</c> (API version segment <c>2025-12-01</c>):
/// <list type="bullet">
///   <item><c>POST /2025-12-01/durable-executions/{arn}/checkpoint</c></item>
///   <item><c>GET  /2025-12-01/durable-executions/{arn}/state</c></item>
/// </list>
/// The <c>DurableExecutionArn</c> contains slashes
/// (<c>arn:…:function:NAME:QUALIFIER/durable-execution/GROUP/NAME</c>) and arrives URL-encoded,
/// so a catch-all route captures the whole tail and the suffix (<c>/checkpoint</c>, <c>/state</c>)
/// is split off in code — mirroring the API Gateway emulator's <c>{**catchAll}</c> pattern.
/// </remarks>
internal sealed class DurableServiceApi
{
    internal const string ApiVersion = "2025-12-01";
    private const string RoutePrefix = "/" + ApiVersion + "/durable-executions";
    private const string CallbackRoutePrefix = "/" + ApiVersion + "/durable-execution-callbacks";

    // Case-insensitive to match the SDK's PascalCase members regardless of any future casing drift.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly DurableExecutionStore _store;
    // Present only when the control plane (start hook + drive loop) is enabled; the callback
    // endpoints wake the parked drive loop after resolving a callback.
    private readonly DurableExecutionDriver? _driver;

    private DurableServiceApi(WebApplication app)
    {
        _store = app.Services.GetRequiredService<DurableExecutionStore>();
        _driver = app.Services.GetService<DurableExecutionDriver>();

        // A single catch-all captures "{arn-with-slashes}/{suffix}". ASP.NET Core decodes %2F in
        // {**tail} back to '/', so the raw ARN is reconstructed transparently.
        app.MapPost(RoutePrefix + "/{**tail}", HandleDurableExecutionPost);
        app.MapGet(RoutePrefix + "/{**tail}", HandleDurableExecutionGet);

        // Callback endpoints: {CallbackId}/{succeed|fail|heartbeat}.
        app.MapPost(CallbackRoutePrefix + "/{callbackId}/{action}", HandleCallbackPost);
    }

    public static void SetupDurableServiceEndpoints(WebApplication app)
    {
        _ = new DurableServiceApi(app);
    }

    private async Task HandleDurableExecutionPost(HttpContext ctx, string tail)
    {
        if (!TrySplit(tail, "checkpoint", out var arn))
        {
            await WriteErrorAsync(ctx, HttpStatusCode.NotFound, "ResourceNotFoundException",
                $"Unsupported durable-executions POST path: '{tail}'.");
            return;
        }

        await HandleCheckpointAsync(ctx, arn);
    }

    private async Task HandleDurableExecutionGet(HttpContext ctx, string tail)
    {
        if (!TrySplit(tail, "state", out var arn))
        {
            await WriteErrorAsync(ctx, HttpStatusCode.NotFound, "ResourceNotFoundException",
                $"Unsupported durable-executions GET path: '{tail}'.");
            return;
        }

        await HandleGetStateAsync(ctx, arn);
    }

    private async Task HandleCheckpointAsync(HttpContext ctx, string arn)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync();

        CheckpointRequestBody? request;
        try
        {
            request = JsonSerializer.Deserialize<CheckpointRequestBody>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            await WriteErrorAsync(ctx, HttpStatusCode.BadRequest, "InvalidRequestContentException",
                $"Could not parse checkpoint request body: {ex.Message}");
            return;
        }

        if (request is null)
        {
            await WriteErrorAsync(ctx, HttpStatusCode.BadRequest, "InvalidRequestContentException",
                "Checkpoint request body was empty.");
            return;
        }

        var (newToken, newOps) = _store.Checkpoint(arn, request.CheckpointToken, request.Updates);

        var response = new CheckpointResponseBody
        {
            CheckpointToken = newToken,
            NewExecutionState = new NewExecutionStateBody
            {
                Operations = newOps.Select(WireOperation.From).ToList(),
                NextMarker = null
            }
        };

        await WriteJsonAsync(ctx, HttpStatusCode.OK, response);
    }

    private async Task HandleGetStateAsync(HttpContext ctx, string arn)
    {
        var marker = ctx.Request.Query.TryGetValue("Marker", out var m) ? m.ToString() : null;

        var (operations, nextMarker) = _store.GetState(arn, marker);

        var response = new StateResponseBody
        {
            Operations = operations.Select(WireOperation.From).ToList(),
            NextMarker = nextMarker
        };

        await WriteJsonAsync(ctx, HttpStatusCode.OK, response);
    }

    private async Task HandleCallbackPost(HttpContext ctx, string callbackId, string action)
    {
        // succeed: raw body is the result payload (BinaryOperationPayload).
        // fail:    body is a JSON ErrorObject.
        // heartbeat: no body; an ack that resets the callback's heartbeat timer (a no-op here).
        string? result = null;
        Amazon.Lambda.DurableExecution.ErrorObject? error = null;

        switch (action)
        {
            case "succeed":
            {
                using var reader = new StreamReader(ctx.Request.Body);
                result = await reader.ReadToEndAsync();
                break;
            }
            case "fail":
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                var wire = string.IsNullOrEmpty(body) ? null : SafeDeserialize<WireErrorObject>(body);
                error = new Amazon.Lambda.DurableExecution.ErrorObject
                {
                    ErrorType = wire?.ErrorType ?? "CallbackFailure",
                    ErrorMessage = wire?.ErrorMessage,
                    ErrorData = wire?.ErrorData,
                    StackTrace = wire?.StackTrace
                };
                break;
            }
            case "heartbeat":
            {
                // Acknowledge only if the callback is known; nothing to mutate.
                if (!_store.CallbackExists(callbackId))
                {
                    await WriteErrorAsync(ctx, HttpStatusCode.NotFound, "ResourceNotFoundException",
                        $"Unknown callback id '{callbackId}'.");
                    return;
                }
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                return;
            }
            default:
                await WriteErrorAsync(ctx, HttpStatusCode.NotFound, "ResourceNotFoundException",
                    $"Unsupported callback action '{action}'.");
                return;
        }

        var (outcome, arn) = _store.ResolveCallback(callbackId, result, error);
        switch (outcome)
        {
            case DurableExecutionStore.CallbackResolution.UnknownCallback:
                await WriteErrorAsync(ctx, HttpStatusCode.NotFound, "ResourceNotFoundException",
                    $"Unknown callback id '{callbackId}'.");
                return;
            case DurableExecutionStore.CallbackResolution.AlreadyResolved:
                await WriteErrorAsync(ctx, HttpStatusCode.Conflict, "CallbackAlreadyCompletedException",
                    $"Callback '{callbackId}' has already been completed.");
                return;
            case DurableExecutionStore.CallbackResolution.Resolved:
                if (arn is not null)
                    _driver?.NotifyCallbackResolved(arn);
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                return;
        }
    }

    private static T? SafeDeserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }

    /// <summary>
    /// Splits a catch-all tail of the form "{arn}/{suffix}" into its ARN and validates the
    /// suffix. The ARN itself contains slashes, so we match on the LAST segment.
    /// </summary>
    private static bool TrySplit(string tail, string expectedSuffix, out string arn)
    {
        arn = string.Empty;
        if (string.IsNullOrEmpty(tail))
            return false;

        var lastSlash = tail.LastIndexOf('/');
        if (lastSlash <= 0 || lastSlash == tail.Length - 1)
            return false;

        var suffix = tail[(lastSlash + 1)..];
        if (!string.Equals(suffix, expectedSuffix, StringComparison.Ordinal))
            return false;

        // The ARN's own slashes arrive percent-encoded (%2F) and ASP.NET's catch-all preserves
        // them un-decoded, whereas the driver keys the store by the raw ARN. Unescape so both
        // sides agree on the key. The suffix separator itself is a genuine '/', so the split
        // point above is unaffected.
        arn = Uri.UnescapeDataString(tail[..lastSlash]);
        return true;
    }

    private static async Task WriteJsonAsync<T>(HttpContext ctx, HttpStatusCode status, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength = bytes.Length;
        await ctx.Response.Body.WriteAsync(bytes);
    }

    private static async Task WriteErrorAsync(HttpContext ctx, HttpStatusCode status, string errorType, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message }, JsonOptions));
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";
        // AWSSDK reads the error type from this header (rest-json protocol).
        ctx.Response.Headers["X-Amzn-Errortype"] = errorType;
        ctx.Response.ContentLength = bytes.Length;
        await ctx.Response.Body.WriteAsync(bytes);
    }
}
