using System.IO;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using SdkErrorObject = Amazon.Lambda.Model.ErrorObject;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable child context operation. Runs a user-supplied function inside a
/// nested <see cref="DurableContext"/> with its own deterministic operation-ID
/// space, persisting the function's result so subsequent invocations replay
/// the cached value without re-executing.
/// </summary>
/// <remarks>
/// Replay branches — example: <c>await ctx.RunInChildContextAsync(child =&gt; ..., name: "phase")</c>
/// <list type="bullet">
///   <item><b>Fresh</b>: no prior state → sync-flush CONTEXT START → run user
///       func → on success emit CONTEXT SUCCEED → on failure emit CONTEXT FAIL
///       and throw <see cref="ChildContextException"/>.</item>
///   <item><b>SUCCEEDED</b>: return cached deserialized result; user func is
///       NOT re-executed.</item>
///   <item><b>FAILED</b>: throw <see cref="ChildContextException"/> with the
///       recorded error; if <see cref="ChildContextConfig.ErrorMapping"/> is
///       set, the mapped exception is thrown instead.</item>
///   <item><b>STARTED</b> / <b>PENDING</b>: re-run the user func without
///       re-checkpointing START. The child's own operations recover from their
///       own checkpoints, so this is replay propagation; if a wait/callback
///       inside the child is still pending, the user func re-suspends.</item>
/// </list>
/// Unlike <see cref="StepOperation{T}"/>, child contexts have no retry strategy:
/// failure is terminal and surfaces immediately via
/// <see cref="ChildContextException"/>.
/// </remarks>
internal sealed class ChildContextOperation<T> : DurableOperation<T>
{
    private readonly Func<IDurableContext, Task<T>> _func;
    private readonly ChildContextConfig? _config;
    private readonly ILambdaSerializer _serializer;
    private readonly Func<string, IDurableContext> _childContextFactory;

    public ChildContextOperation(
        string operationId,
        string? name,
        string? parentId,
        Func<IDurableContext, Task<T>> func,
        ChildContextConfig? config,
        ILambdaSerializer serializer,
        Func<string, IDurableContext> childContextFactory,
        ExecutionState state,
        TerminationManager termination,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, parentId, state, termination, durableExecutionArn, batcher)
    {
        _func = func;
        _config = config;
        _serializer = serializer;
        _childContextFactory = childContextFactory;
    }

    protected override string OperationType => OperationTypes.Context;

    protected override async Task<T> StartAsync(CancellationToken cancellationToken)
    {
        // Sync-flush CONTEXT START before user code so the service has a record
        // of the parent context if the inner func suspends (e.g. a Wait inside
        // the child terminates the workflow before SUCCEED is reached).
        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            ParentId = ParentId,
            Type = OperationTypes.Context,
            Action = OperationAction.START,
            SubType = _config?.SubType,
            Name = Name
        }, cancellationToken);

        return await ExecuteFunc(cancellationToken);
    }

    protected override Task<T> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
                // Side-effecting code runs at most once: replay returns the
                // cached result without invoking the user func.
                return Task.FromResult(DeserializeResult(existing.ContextDetails?.Result));

            case OperationStatuses.Failed:
                throw MapFailureException(BuildChildContextException(existing));

            case OperationStatuses.Started:
            case OperationStatuses.Pending:
                // Re-run the user func: the child's own operations replay from
                // their own checkpoints. Do NOT re-checkpoint START — the
                // original is still authoritative. If something inside the
                // child is still pending (Wait, callback, retry) the user func
                // will re-suspend on its own.
                return ExecuteFunc(cancellationToken);

            default:
                throw new NonDeterministicExecutionException(
                    $"Child context operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }
    }

    private async Task<T> ExecuteFunc(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var childContext = _childContextFactory(OperationId);

        T result;
        try
        {
            result = await _func(childContext);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Context,
                Action = OperationAction.FAIL,
                SubType = _config?.SubType,
                Name = Name,
                Error = ToSdkError(ex)
            }, cancellationToken);

            throw MapFailureException(new ChildContextException(ex.Message, ex)
            {
                SubType = _config?.SubType,
                ErrorType = ex.GetType().FullName
            });
        }

        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            ParentId = ParentId,
            Type = OperationTypes.Context,
            Action = OperationAction.SUCCEED,
            SubType = _config?.SubType,
            Name = Name,
            Payload = SerializeResult(result)
        }, cancellationToken);

        return result;
    }

    private Exception MapFailureException(ChildContextException ex)
    {
        var mapper = _config?.ErrorMapping;
        if (mapper == null) return ex;

        var mapped = mapper(ex);
        return mapped ?? ex;
    }

    private ChildContextException BuildChildContextException(Operation failedOp)
    {
        var err = failedOp.ContextDetails?.Error;
        return new ChildContextException(err?.ErrorMessage ?? "Child context failed")
        {
            SubType = failedOp.SubType ?? _config?.SubType,
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace
        };
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

    private static SdkErrorObject ToSdkError(Exception ex) => new()
    {
        ErrorType = ex.GetType().FullName,
        ErrorMessage = ex.Message,
        StackTrace = ex.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList()
    };
}
