using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Model;
using Amazon.Runtime;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Static helper that wraps a durable workflow function, handling all envelope
/// translation between DurableExecutionInvocationInput/Output and user types.
/// </summary>
public static class DurableFunction
{
    private static readonly Lazy<IAmazonLambda> _cachedLambdaClient =
        new(() => new AmazonLambdaClient(), LazyThreadSafetyMode.ExecutionAndPublication);

    // ──────────────────────────────────────────────────────────────────────
    // Reflection-based overloads (JIT only)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wrap a workflow (typed input + output). Reflection-based JSON — not AOT-safe.
    /// </summary>
    [RequiresUnreferencedCode("Uses reflection-based JSON for TInput/TOutput. Use the JsonSerializerContext overload for AOT.")]
    [RequiresDynamicCode("Uses reflection-based JSON for TInput/TOutput. Use the JsonSerializerContext overload for AOT.")]
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext)
    {
        return WrapAsyncCore(workflow, invocationInput, lambdaContext, _cachedLambdaClient.Value, jsonContext: null);
    }

    /// <summary>
    /// Wrap a workflow (typed input + output) with explicit Lambda client.
    /// Reflection-based JSON — not AOT-safe.
    /// </summary>
    [RequiresUnreferencedCode("Uses reflection-based JSON for TInput/TOutput. Use the JsonSerializerContext overload for AOT.")]
    [RequiresDynamicCode("Uses reflection-based JSON for TInput/TOutput. Use the JsonSerializerContext overload for AOT.")]
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient)
        => WrapAsyncCore(workflow, invocationInput, lambdaContext, lambdaClient, jsonContext: null);

    /// <summary>
    /// Wrap a void workflow (typed input, no output). Reflection-based JSON — not AOT-safe.
    /// </summary>
    [RequiresUnreferencedCode("Uses reflection-based JSON for TInput. Use the JsonSerializerContext overload for AOT.")]
    [RequiresDynamicCode("Uses reflection-based JSON for TInput. Use the JsonSerializerContext overload for AOT.")]
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
        Func<TInput, IDurableContext, Task> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext)
    {
        return WrapAsync(workflow, invocationInput, lambdaContext, _cachedLambdaClient.Value);
    }

    /// <summary>
    /// Wrap a void workflow with explicit Lambda client. Reflection-based JSON — not AOT-safe.
    /// </summary>
    [RequiresUnreferencedCode("Uses reflection-based JSON for TInput. Use the JsonSerializerContext overload for AOT.")]
    [RequiresDynamicCode("Uses reflection-based JSON for TInput. Use the JsonSerializerContext overload for AOT.")]
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
        Func<TInput, IDurableContext, Task> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient)
        => WrapAsyncCore<TInput, object?>(
            async (input, ctx) => { await workflow(input, ctx); return null; },
            invocationInput, lambdaContext, lambdaClient, jsonContext: null);

    // ──────────────────────────────────────────────────────────────────────
    // AOT-safe overloads (caller supplies JsonSerializerContext)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wrap a workflow (typed input + output). AOT-safe — requires
    /// <c>[JsonSerializable(typeof(TInput))]</c> and <c>[JsonSerializable(typeof(TOutput))]</c>
    /// on the supplied <paramref name="jsonContext"/>.
    /// </summary>
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        JsonSerializerContext jsonContext)
    {
        return WrapAsyncCore(workflow, invocationInput, lambdaContext, _cachedLambdaClient.Value, jsonContext);
    }

    /// <summary>
    /// Wrap a workflow (typed input + output) with explicit Lambda client. AOT-safe.
    /// </summary>
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient,
        JsonSerializerContext jsonContext)
        => WrapAsyncCore(workflow, invocationInput, lambdaContext, lambdaClient, jsonContext);

    /// <summary>
    /// Wrap a void workflow (typed input, no output). AOT-safe.
    /// </summary>
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
        Func<TInput, IDurableContext, Task> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        JsonSerializerContext jsonContext)
    {
        return WrapAsyncCore<TInput, object?>(
            async (input, ctx) => { await workflow(input, ctx); return null; },
            invocationInput, lambdaContext, _cachedLambdaClient.Value, jsonContext);
    }

    /// <summary>
    /// Wrap a void workflow with explicit Lambda client. AOT-safe.
    /// </summary>
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
        Func<TInput, IDurableContext, Task> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient,
        JsonSerializerContext jsonContext)
        => WrapAsyncCore<TInput, object?>(
            async (input, ctx) => { await workflow(input, ctx); return null; },
            invocationInput, lambdaContext, lambdaClient, jsonContext);

    // ──────────────────────────────────────────────────────────────────────
    // Core implementation
    // ──────────────────────────────────────────────────────────────────────

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "When jsonContext is non-null, dispatch goes through JsonTypeInfo<T>; when null, the caller has [RequiresUnreferencedCode].")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "When jsonContext is non-null, dispatch goes through JsonTypeInfo<T>; when null, the caller has [RequiresDynamicCode].")]
    private static async Task<DurableExecutionInvocationOutput> WrapAsyncCore<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient,
        JsonSerializerContext? jsonContext)
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(invocationInput.InitialExecutionState);

        var serviceClient = new LambdaDurableServiceClient(lambdaClient);
        var checkpointToken = invocationInput.CheckpointToken;

        var nextMarker = invocationInput.InitialExecutionState?.NextMarker;
        while (!string.IsNullOrEmpty(nextMarker))
        {
            var (operations, marker) = await serviceClient.GetExecutionStateAsync(
                invocationInput.DurableExecutionArn, checkpointToken, nextMarker);
            state.AddOperations(operations);
            nextMarker = marker;
        }

        var userPayload = ExtractUserPayload<TInput>(invocationInput, jsonContext);
        var terminationManager = new TerminationManager();
        var idGenerator = new OperationIdGenerator();

        await using var batcher = new CheckpointBatcher(
            checkpointToken,
            (token, ops, ct) => serviceClient.CheckpointAsync(
                invocationInput.DurableExecutionArn, token, ops, ct));

        var context = new DurableContext(
            state, terminationManager, idGenerator,
            invocationInput.DurableExecutionArn, lambdaContext, batcher);

        HandlerResult<TOutput> result;
        try
        {
            result = await DurableExecutionHandler.RunAsync<TOutput>(
                state, terminationManager,
                async () => await workflow(userPayload, context));

            await batcher.DrainAsync();
        }
        catch (AmazonServiceException ex) when (IsTerminalCheckpointError(ex))
        {
            return new DurableExecutionInvocationOutput
            {
                Status = InvocationStatus.Failed,
                Error = ErrorObject.FromException(ex)
            };
        }

        return MapToOutput(result, jsonContext);
    }

    /// <summary>
    /// Returns true for checkpoint-flush SDK errors that should fail the workflow
    /// (Failed envelope) instead of escaping to the host (Lambda retry).
    /// </summary>
    /// <remarks>
    /// Classification rule (mirrors <c>CheckpointError</c> in aws-durable-execution-sdk-python):
    ///   - 4xx (except 429) → terminal: permanent caller-side failure (missing ARN/KMS key,
    ///     IAM denial, validation). Retrying will not fix it, so return Failed.
    ///   - 429 / 5xx / no status (network or SDK-internal) → not terminal: transient,
    ///     allow the exception to escape so Lambda retries the invocation.
    ///   - Carve-out: <c>InvalidParameterValueException</c> with a message starting with
    ///     "Invalid Checkpoint Token" is treated as transient — the service rejects a
    ///     stale token but a retry with a fresh token will succeed.
    ///
    /// Only checkpoint-flush errors flow through this catch. There are two paths:
    ///   1. A flush triggered synchronously from inside a user <c>StepAsync</c> call
    ///      (the user awaits <c>EnqueueAsync</c> → batch flush → SDK throws).
    ///   2. The final <see cref="CheckpointBatcher.DrainAsync"/> after the workflow returns.
    ///
    /// State-hydration errors (<c>GetExecutionStateAsync</c>) are NOT caught here — they
    /// propagate to the host so Lambda retries, matching Python's <c>GetExecutionStateError</c>
    /// (which extends <c>InvocationError</c>).
    ///
    /// User-code SDK errors (e.g. an SDK call inside a Step body) are caught by
    /// <c>StepRunner</c> and surfaced as <c>StepException</c> for the workflow's normal
    /// step-failure handling.
    /// </remarks>
    private static bool IsTerminalCheckpointError(AmazonServiceException ex)
    {
        var status = (int)ex.StatusCode;
        if (status < 400 || status >= 500 || status == 429)
            return false;

        if (ex.ErrorCode == "InvalidParameterValueException"
            && ex.Message != null
            && ex.Message.StartsWith("Invalid Checkpoint Token", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    // Shared options for both user-payload deserialization (input) and user-result
    // serialization (output) so the naming policy stays symmetric. We only enable
    // case-insensitive matching here — keep PascalCase on the wire for output to
    // preserve compatibility with existing serialized contracts. Only the user payload
    // portion uses these options; the durable-execution envelope itself
    // (DurableExecutionInvocationInput/Output) is serialized separately and is not
    // affected.
    private static readonly JsonSerializerOptions UserPayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Guarded by jsonContext null check.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded by jsonContext null check.")]
    // The user's input payload is stored inside the service envelope as an EXECUTION-type
    // operation. This is part of the durable execution wire format — each invocation includes
    // its input as a checkpoint record so the service can validate replay consistency.
    private static TInput ExtractUserPayload<TInput>(
        DurableExecutionInvocationInput input,
        JsonSerializerContext? jsonContext)
    {
        if (input.InitialExecutionState?.Operations == null)
            return default!;

        foreach (var op in input.InitialExecutionState.Operations)
        {
            if (op.Type != OperationTypes.Execution || op.ExecutionDetails?.InputPayload == null)
                continue;

            var payload = op.ExecutionDetails.InputPayload;
            if (jsonContext != null)
            {
                if (jsonContext.GetTypeInfo(typeof(TInput)) is JsonTypeInfo<TInput> typeInfo)
                    return JsonSerializer.Deserialize(payload, typeInfo) ?? default!;

                throw new InvalidOperationException(
                    $"JsonSerializerContext {jsonContext.GetType().FullName} has no JsonTypeInfo for {typeof(TInput).FullName}. " +
                    "Add [JsonSerializable(typeof(YourInput))] to your context.");
            }

            return JsonSerializer.Deserialize<TInput>(payload, UserPayloadOptions) ?? default!;
        }

        return default!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Guarded by jsonContext null check.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded by jsonContext null check.")]
    private static DurableExecutionInvocationOutput MapToOutput<TOutput>(
        HandlerResult<TOutput> result,
        JsonSerializerContext? jsonContext)
    {
        return result.Status switch
        {
            InvocationStatus.Succeeded => new DurableExecutionInvocationOutput
            {
                Status = InvocationStatus.Succeeded,
                Result = SerializeOutput(result.Result, jsonContext)
            },
            InvocationStatus.Failed => new DurableExecutionInvocationOutput
            {
                Status = InvocationStatus.Failed,
                Error = result.Exception != null
                    ? ErrorObject.FromException(result.Exception)
                    : new ErrorObject { ErrorMessage = result.Message }
            },
            // Pending = workflow suspended (wait/retry/callback). No Result or Error —
            // the service will re-invoke with accumulated checkpoints when ready.
            InvocationStatus.Pending => new DurableExecutionInvocationOutput
            {
                Status = InvocationStatus.Pending
            },
            _ => throw new InvalidOperationException($"Unexpected status: {result.Status}")
        };
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Guarded by jsonContext null check.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded by jsonContext null check.")]
    private static string? SerializeOutput<TOutput>(TOutput? value, JsonSerializerContext? jsonContext)
    {
        if (value == null) return null;

        if (jsonContext != null)
        {
            if (jsonContext.GetTypeInfo(typeof(TOutput)) is JsonTypeInfo<TOutput> typeInfo)
                return JsonSerializer.Serialize(value, typeInfo);

            throw new InvalidOperationException(
                $"JsonSerializerContext {jsonContext.GetType().FullName} has no JsonTypeInfo for {typeof(TOutput).FullName}. " +
                "Add [JsonSerializable(typeof(YourOutput))] to your context.");
        }

        return JsonSerializer.Serialize(value, UserPayloadOptions);
    }
}
