using System.IO;
using System.Text;
using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Model;
using Amazon.Runtime;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Shared orchestration body for <see cref="DurableEntryPoint{TInput,TOutput}"/>.
/// Reads the envelope with the library context, runs the workflow with the user's
/// serializer for <c>TInput</c>/<c>TOutput</c>, returns the populated output envelope.
/// </summary>
internal static class DurableEntryPointCore
{
    public static async Task<DurableExecutionInvocationOutput> InvokeAsync<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        Stream input,
        ILambdaContext lambdaContext,
        IAmazonLambda lambdaClient)
    {
        var serializer = lambdaContext.Serializer
            ?? throw new InvalidOperationException(
                "No ILambdaSerializer is registered on ILambdaContext.Serializer. " +
                "In the class library programming model, register one with " +
                "[assembly: LambdaSerializer(typeof(...))]. In an executable / custom " +
                "runtime, pass it to LambdaBootstrapBuilder.Create(handler, serializer). " +
                "In tests, set TestLambdaContext.Serializer.");

        var invocationInput = JsonSerializer.Deserialize(input, DurableEnvelopeJsonContext.Default.DurableExecutionInvocationInput)
            ?? throw new DurableExecutionException("Durable execution envelope is malformed: input stream produced a null envelope.");

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
    /// Classification rule (mirrors <c>CheckpointError</c> in aws-durable-execution-sdk-python):
    ///   - 4xx (except 429) → terminal: permanent caller-side failure (missing ARN/KMS key,
    ///     IAM denial, validation). Retrying will not fix it, so return Failed.
    ///   - 429 / 5xx / no status (network or SDK-internal) → not terminal: transient,
    ///     allow the exception to escape so Lambda retries the invocation.
    ///   - Carve-out: <c>InvalidParameterValueException</c> with a message starting with
    ///     "Invalid Checkpoint Token" is treated as transient — the service rejects a
    ///     stale token but a retry with a fresh token will succeed.
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
