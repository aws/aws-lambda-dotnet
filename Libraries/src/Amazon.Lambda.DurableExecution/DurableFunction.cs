// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using System.Threading;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Static helper that wraps a durable workflow function, handling all envelope
/// translation between DurableExecutionInvocationInput/Output and user types.
///
/// All four overloads dispatch through the <see cref="ILambdaSerializer"/> registered
/// on <see cref="ILambdaContext.Serializer"/>, so AOT-safe and reflection-based
/// callers share a single code path. Callers wire AOT support by registering an
/// AOT-aware serializer with the runtime
/// (e.g., <c>SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;</c>) — no per-call
/// <c>JsonSerializerContext</c> argument is required.
/// </summary>
public static class DurableFunction
{
    private static readonly Lazy<IAmazonLambda> _cachedLambdaClient =
        new(() => new AmazonLambdaClient(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Wrap a workflow (typed input + output).
    /// </summary>
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext)
        => WrapAsyncCore(workflow, invocationInput, lambdaContext, _cachedLambdaClient.Value);

    /// <summary>
    /// Wrap a workflow (typed input + output) with explicit Lambda client.
    /// </summary>
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient)
        => WrapAsyncCore(workflow, invocationInput, lambdaContext, lambdaClient);

    /// <summary>
    /// Wrap a void workflow (typed input, no output).
    /// </summary>
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
        Func<TInput, IDurableContext, Task> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext)
        => WrapAsync(workflow, invocationInput, lambdaContext, _cachedLambdaClient.Value);

    /// <summary>
    /// Wrap a void workflow with explicit Lambda client.
    /// </summary>
    public static Task<DurableExecutionInvocationOutput> WrapAsync<TInput>(
        Func<TInput, IDurableContext, Task> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient)
        => WrapAsyncCore<TInput, object?>(
            async (input, ctx) => { await workflow(input, ctx); return null; },
            invocationInput, lambdaContext, lambdaClient);

    private static async Task<DurableExecutionInvocationOutput> WrapAsyncCore<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput invocationInput,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient)
    {
        var serializer = LambdaSerializerHelper.GetRequired(lambdaContext);

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

        var userPayload = ExtractUserPayload<TInput>(invocationInput, serializer);
        var terminationManager = new TerminationManager();
        using var workflowCancellation = new WorkflowCancellation(terminationManager);
        var idGenerator = new OperationIdGenerator();

        await using var batcher = new CheckpointBatcher(
            checkpointToken,
            (token, ops, ct) => serviceClient.CheckpointAsync(
                invocationInput.DurableExecutionArn, token, ops,
                // The service stamps a freshly-allocated CallbackId onto a started
                // CALLBACK op (and may emit terminal-state callbacks/timers); merge
                // those back into ExecutionState so the next ExecuteAsync sees them.
                onNewOperations: state.AddOperations,
                cancellationToken: ct));

        var context = new DurableContext(
            state, terminationManager, workflowCancellation, idGenerator,
            invocationInput.DurableExecutionArn, lambdaContext, batcher);

        HandlerResult<TOutput> result;
        try
        {
            // Push execution-level metadata into a logging scope so structured
            // providers (the runtime's JSON formatter, Serilog, Powertools,
            // etc.) tag every log line emitted by user code with the
            // execution ARN and request id. The key is "executionArn" because
            // that is the field the Lambda console filters on when rendering an
            // execution's logs; "durableExecutionArn" caused logs to be dropped
            // from the console view (issue #2423).
            using (context.Logger.BeginScope(new Dictionary<string, object>
            {
                ["executionArn"] = invocationInput.DurableExecutionArn,
                ["awsRequestId"] = lambdaContext.AwsRequestId ?? string.Empty,
            }))
            {
                result = await DurableExecutionHandler.RunAsync<TOutput>(
                    state, terminationManager,
                    async () => await workflow(userPayload, context));
            }

            await batcher.DrainAsync();
        }
        catch (DurableExecutionException ex) when (ex.InnerException is AmazonServiceException sdkEx && IsTerminalCheckpointError(sdkEx))
        {
            return new DurableExecutionInvocationOutput
            {
                Status = InvocationStatus.Failed,
                Error = ErrorObject.FromException(ex)
            };
        }

        return MapToOutput(result, serializer);
    }

    /// <summary>
    /// Returns true for checkpoint-flush SDK errors that should fail the workflow
    /// (Failed envelope) instead of escaping to the host (Lambda retry). The catch
    /// site unwraps a <see cref="DurableExecutionException"/> first because
    /// <see cref="Services.LambdaDurableServiceClient"/> wraps every SDK error so
    /// user logs show durable-execution context — this method then classifies the
    /// inner <see cref="AmazonServiceException"/>.
    /// </summary>
    /// <remarks>
    /// Classification rule:
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
    ///      (the user awaits <c>EnqueueAsync</c> → batch flush → SDK throws → service client
    ///      wraps).
    ///   2. The final <see cref="CheckpointBatcher.DrainAsync"/> after the workflow returns.
    ///
    /// State-hydration errors (<c>GetExecutionStateAsync</c>) propagate as
    /// <see cref="DurableExecutionException"/> too, but they are NOT caught here — they
    /// flow up to the host so Lambda retries.
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

    // The user's input payload is stored inside the service envelope as an EXECUTION-type
    // operation. This is part of the durable execution wire format — each invocation includes
    // its input as a checkpoint record so the service can validate replay consistency.
    // A missing EXECUTION op is a malformed envelope: surfacing it as a typed exception here
    // gives a clear error instead of letting default!/null bubble into user code as an opaque
    // NullReferenceException.
    private static TInput ExtractUserPayload<TInput>(
        DurableExecutionInvocationInput input,
        ILambdaSerializer serializer)
    {
        if (input.InitialExecutionState?.Operations != null)
        {
            foreach (var op in input.InitialExecutionState.Operations)
            {
                if (op.Type != OperationTypes.Execution || op.ExecutionDetails?.InputPayload == null)
                    continue;

                var payload = op.ExecutionDetails.InputPayload;
                var bytes = Encoding.UTF8.GetBytes(payload);
                using var ms = new MemoryStream(bytes);
                return serializer.Deserialize<TInput>(ms);
            }
        }

        throw new DurableExecutionException(
            "Durable execution envelope is malformed: no EXECUTION-type operation with an input payload was found. " +
            "The service must include an EXECUTION op carrying the workflow's input on every invocation.");
    }

    private static DurableExecutionInvocationOutput MapToOutput<TOutput>(
        HandlerResult<TOutput> result,
        ILambdaSerializer serializer)
    {
        return result.Status switch
        {
            InvocationStatus.Succeeded => new DurableExecutionInvocationOutput
            {
                Status = InvocationStatus.Succeeded,
                Result = SerializeOutput(result.Result, serializer)
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

    private static string? SerializeOutput<TOutput>(TOutput? value, ILambdaSerializer serializer)
    {
        if (value == null) return null;

        using var ms = new MemoryStream();
        serializer.Serialize(value, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
