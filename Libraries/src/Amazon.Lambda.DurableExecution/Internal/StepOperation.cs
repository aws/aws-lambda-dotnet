using System.IO;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using SdkErrorObject = Amazon.Lambda.Model.ErrorObject;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable step operation. Runs the user's function once across the lifetime
/// of a durable execution, persisting its result so subsequent invocations
/// replay the cached value without re-executing.
/// </summary>
/// <remarks>
/// Replay semantics — example: <c>await ctx.StepAsync(ChargeCard, "charge")</c>
/// <list type="bullet">
///   <item>Fresh: no prior state → run func → emit SUCCEED → return result.</item>
///   <item>Replay (SUCCEEDED): return cached result; func is NOT re-executed.</item>
///   <item>Replay (FAILED): re-throw the recorded exception.</item>
/// </list>
/// Serialization is delegated to the <see cref="ILambdaSerializer"/> registered on
/// <see cref="ILambdaContext.Serializer"/>. AOT-safe and reflection-based callers
/// share the same code path: the AOT story is determined entirely by the serializer
/// the user registered with the runtime (e.g.,
/// <c>SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;</c>).
/// </remarks>
internal sealed class StepOperation<T> : DurableOperation<T>
{
    private readonly Func<IStepContext, Task<T>> _func;
    private readonly StepConfig? _config;
    private readonly ILambdaSerializer _serializer;
    private readonly ILogger _logger;

    public StepOperation(
        string operationId,
        string? name,
        Func<IStepContext, Task<T>> func,
        StepConfig? config,
        ILambdaSerializer serializer,
        ILogger logger,
        ExecutionState state,
        TerminationManager termination,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, state, termination, durableExecutionArn, batcher)
    {
        _func = func;
        _config = config;
        _serializer = serializer;
        _logger = logger;
    }

    protected override string OperationType => OperationTypes.Step;

    protected override Task<T> StartAsync(CancellationToken cancellationToken)
        => ExecuteFunc(cancellationToken);

    protected override Task<T> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
                // Side-effecting code runs at most once: replay returns the
                // cached result without invoking func.
                return Task.FromResult(DeserializeResult(existing.StepDetails?.Result));

            case OperationStatuses.Failed:
                // Retries were exhausted or never configured — re-throw so the
                // user's catch-block flow matches the original execution.
                throw CreateStepException(existing);

            default:
                // STARTED/READY/PENDING from a prior invocation — no retry logic
                // in this commit, so fall through and execute fresh. (Future work
                // on retries will replace this default with explicit arms.)
                return ExecuteFunc(cancellationToken);
        }
    }

    private async Task<T> ExecuteFunc(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // TODO: emit a STEP_STARTED checkpoint (action = "START") here when retries
        // and/or AtMostOncePerRetry semantics land. AtMostOncePerRetry needs the
        // START to be sync-flushed before user code runs (so replay can detect
        // "we already attempted this and must not re-run"). AtLeastOncePerRetry
        // wants it fire-and-forget for telemetry (attempt timing, retry count in
        // history). Both require the async-flush overload in CheckpointBatcher
        // (see TODO in CheckpointBatcher.cs). Today neither feature is wired up,
        // so the START is intentionally omitted — SUCCEED alone is sufficient
        // for replay correctness in the AtLeastOncePerRetry-only world this PR
        // ships. Java SDK precedent: StepOperation.checkpointStarted().
        try
        {
            var stepContext = new StepContext(OperationId, attemptNumber: 1, _logger);
            var result = await _func(stepContext);

            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                Type = OperationTypes.Step,
                Action = OperationAction.SUCCEED,
                SubType = "Step",
                Name = Name,
                Payload = SerializeResult(result)
            }, cancellationToken);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // No retry logic in this commit: any thrown exception becomes a
            // FAIL checkpoint and is re-thrown as a StepException. On replay,
            // the FAILED branch above will re-throw without re-executing.
            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                Type = OperationTypes.Step,
                Action = OperationAction.FAIL,
                SubType = "Step",
                Name = Name,
                Error = ToSdkError(ex)
            }, cancellationToken);

            throw new StepException(ex.Message, ex)
            {
                ErrorType = ex.GetType().FullName
            };
        }
    }

    private T DeserializeResult(string? serialized)
    {
        if (serialized == null) return default!;
        var bytes = Encoding.UTF8.GetBytes(serialized);
        using var ms = new MemoryStream(bytes);
        return _serializer.Deserialize<T>(ms);
    }

    private string SerializeResult(T value)
    {
        using var ms = new MemoryStream();
        _serializer.Serialize(value, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static StepException CreateStepException(Operation failedOp)
    {
        var err = failedOp.StepDetails?.Error;
        return new StepException(err?.ErrorMessage ?? "Step failed")
        {
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace
        };
    }

    private static SdkErrorObject ToSdkError(Exception ex) => new()
    {
        ErrorType = ex.GetType().FullName,
        ErrorMessage = ex.Message,
        StackTrace = ex.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList()
    };
}
