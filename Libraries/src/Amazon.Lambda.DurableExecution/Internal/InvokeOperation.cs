using System.IO;
using System.Text;
using Amazon.Lambda.Core;
using SdkChainedInvokeOptions = Amazon.Lambda.Model.ChainedInvokeOptions;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable chained-invoke operation. Schedules an asynchronous invocation of
/// another durable Lambda function via the durable execution service and
/// suspends the parent workflow until the chained execution reaches a terminal
/// state. The service drives the chained function and re-invokes the parent
/// with an updated operation status.
/// </summary>
/// <remarks>
/// Replay branches — example:
/// <c>await ctx.InvokeAsync&lt;Req, Resp&gt;("arn:...:fn:prod", req, "process_payment")</c>
/// <list type="bullet">
///   <item><b>Fresh</b>: serialize payload → sync-flush <c>CHAINED_INVOKE START</c>
///       (carrying <see cref="SdkChainedInvokeOptions"/>) → suspend with
///       <see cref="TerminationReason.InvokePending"/>.</item>
///   <item><b>SUCCEEDED</b>: deserialize and return cached result from
///       <c>ChainedInvokeDetails.Result</c>; the chained function is NOT
///       re-invoked.</item>
///   <item><b>FAILED</b>: throw <see cref="InvokeFailedException"/> populated
///       from the recorded error.</item>
///   <item><b>TIMED_OUT</b>: throw <see cref="InvokeTimedOutException"/>.</item>
///   <item><b>STOPPED</b>: throw <see cref="InvokeStoppedException"/>.</item>
///   <item><b>STARTED</b> / <b>PENDING</b>: chained execution is still in
///       flight; re-suspend without re-checkpointing — the original
///       <c>START</c> remains authoritative.</item>
/// </list>
/// Mirrors <see cref="WaitOperation"/>'s "sync-flush START → suspend" idiom;
/// the chained function executes out-of-process so there is nothing to run
/// locally on either fresh or replay paths besides the suspend wiring.
/// Serialization is delegated to the <see cref="ILambdaSerializer"/> registered
/// on <see cref="ILambdaContext.Serializer"/>; AOT-safe and reflection-based
/// callers share the same code path (the AOT story is determined by the
/// registered serializer).
/// </remarks>
internal sealed class InvokeOperation<TPayload, TResult> : DurableOperation<TResult>
{
    private readonly string _functionName;
    private readonly TPayload _payload;
    private readonly InvokeConfig? _config;
    private readonly ILambdaSerializer _serializer;

    public InvokeOperation(
        string operationId,
        string? name,
        string? parentId,
        string functionName,
        TPayload payload,
        InvokeConfig? config,
        ILambdaSerializer serializer,
        ExecutionState state,
        TerminationManager termination,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, parentId, state, termination, durableExecutionArn, batcher)
    {
        _functionName = functionName;
        _payload = payload;
        _config = config;
        _serializer = serializer;
    }

    protected override string OperationType => OperationTypes.ChainedInvoke;

    protected override async Task<TResult> StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var serializedPayload = SerializeValue(_payload);

        // The service is what actually invokes the chained function, so it
        // must receive this START before we suspend. If we only batched it
        // locally and the parent process were recycled at suspend, the START
        // would be lost and the chained function would never run.
        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            ParentId = ParentId,
            Type = OperationTypes.ChainedInvoke,
            Action = OperationAction.START,
            SubType = OperationSubTypes.ChainedInvoke,
            Name = Name,
            Payload = serializedPayload,
            ChainedInvokeOptions = new SdkChainedInvokeOptions
            {
                FunctionName = _functionName,
                TenantId = _config?.TenantId
            }
        }, cancellationToken);

        return await Termination.SuspendAndAwait<TResult>(
            TerminationReason.InvokePending, $"invoke:{Name ?? _functionName}");
    }

    protected override Task<TResult> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
                return Task.FromResult(DeserializeResult(existing.ChainedInvokeDetails?.Result));

            case OperationStatuses.Failed:
                throw BuildFailed(existing);

            case OperationStatuses.TimedOut:
                throw BuildTimedOut(existing);

            case OperationStatuses.Stopped:
                throw BuildStopped(existing);

            case OperationStatuses.Started:
            case OperationStatuses.Pending:
                // Chained function is still running. Just suspend again —
                // the original START is already on the service, so don't
                // re-checkpoint it. Whenever the service re-invokes us next,
                // it will include the updated status.
                return Termination.SuspendAndAwait<TResult>(
                    TerminationReason.InvokePending, $"invoke:{Name ?? _functionName}");

            default:
                throw new NonDeterministicExecutionException(
                    $"Chained invoke operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }
    }

    private string SerializeValue(TPayload value)
    {
        using var ms = new MemoryStream();
        _serializer.Serialize(value, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private TResult DeserializeResult(string? serialized)
    {
        if (serialized == null) return default!;
        var bytes = Encoding.UTF8.GetBytes(serialized);
        using var ms = new MemoryStream(bytes);
        return _serializer.Deserialize<TResult>(ms);
    }

    private InvokeFailedException BuildFailed(Operation failedOp)
    {
        var err = failedOp.ChainedInvokeDetails?.Error;
        return new InvokeFailedException(err?.ErrorMessage ?? "Chained invoke failed.")
        {
            FunctionName = _functionName,
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace
        };
    }

    private InvokeTimedOutException BuildTimedOut(Operation failedOp)
    {
        var err = failedOp.ChainedInvokeDetails?.Error;
        return new InvokeTimedOutException(err?.ErrorMessage ?? "Chained invoke timed out.")
        {
            FunctionName = _functionName,
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace
        };
    }

    private InvokeStoppedException BuildStopped(Operation failedOp)
    {
        var err = failedOp.ChainedInvokeDetails?.Error;
        return new InvokeStoppedException(err?.ErrorMessage ?? "Chained invoke was stopped.")
        {
            FunctionName = _functionName,
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace
        };
    }
}
