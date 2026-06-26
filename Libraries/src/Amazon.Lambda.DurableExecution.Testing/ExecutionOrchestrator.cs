// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Services;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Drives a durable workflow handler to a terminal state by repeatedly invoking
/// the internal DurableFunction.WrapAsync overload with the in-memory service client.
/// </summary>
internal sealed class ExecutionOrchestrator<TInput, TOutput>
{
    private readonly Func<TInput, IDurableContext, Task<TOutput>> _handler;
    private readonly InMemoryOperationStore _store;
    private readonly IDurableServiceClient _serviceClient;
    private readonly ILambdaContext _lambdaContext;
    private readonly TestRunnerOptions _options;
    private readonly ILambdaSerializer _serializer;
    private readonly CheckpointProcessor? _processor;
    private readonly FunctionRegistry? _registry;

    // Accumulates across DriveUntilSuspendedAsync + DriveToTerminalAsync so the
    // reported InvocationCount reflects the whole run (including the invocations
    // consumed before a callback suspension), not just the final drive.
    private int _invocationCount;

    public ExecutionOrchestrator(
        Func<TInput, IDurableContext, Task<TOutput>> handler,
        InMemoryOperationStore store,
        IDurableServiceClient serviceClient,
        ILambdaContext lambdaContext,
        TestRunnerOptions options,
        ILambdaSerializer serializer,
        CheckpointProcessor? processor = null,
        FunctionRegistry? registry = null)
    {
        _handler = handler;
        _store = store;
        _serviceClient = serviceClient;
        _lambdaContext = lambdaContext;
        _options = options;
        _serializer = serializer;
        _processor = processor;
        _registry = registry;
    }

    public Task<TestResult<TOutput>?> DriveUntilSuspendedAsync(
        string arn,
        TInput input,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => DriveAsync(arn, input, timeout, stopAtSuspend: true, cancellationToken);

    public async Task<TestResult<TOutput>> DriveToTerminalAsync(
        string arn,
        TInput input,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var result = await DriveAsync(arn, input, timeout, stopAtSuspend: false, cancellationToken);
        // stopAtSuspend:false only returns null when the workflow suspended on a
        // callback it cannot drive itself — surface that as a clear, actionable error.
        return result ?? throw new InvalidOperationException(
            "Workflow suspended waiting on a callback and cannot be driven to completion with RunAsync. " +
            "Use the two-call pattern instead: StartAsync, then WaitForCallbackAsync + SendCallbackSuccessAsync/SendCallbackFailureAsync, then WaitForResultAsync.");
    }

    private async Task<TestResult<TOutput>?> DriveAsync(
        string arn,
        TInput input,
        TimeSpan timeout,
        bool stopAtSuspend,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        SeedExecutionOperation(arn, input);

        while (true)
        {
            timeoutCts.Token.ThrowIfCancellationRequested();

            if (_invocationCount >= _options.MaxInvocations)
            {
                throw new TestExecutionLimitException(
                    _options.MaxInvocations, _store.OperationCount(arn));
            }

            var invocationInput = BuildInvocationInput(arn);

            var output = await DurableFunction.WrapAsync<TInput, TOutput>(
                _handler, invocationInput, _lambdaContext, _serviceClient);

            _invocationCount++;

            if (output.Status != InvocationStatus.Pending)
                return BuildResult(arn, output, _invocationCount);

            // Pending. Resolve any sibling invokes the workflow started this pass
            // (the runtime suspends on a CHAINED_INVOKE START expecting an external
            // system to run the target); then re-drive so replay sees them resolved.
            if (await ResolvePendingInvokesAsync(arn, timeoutCts.Token))
                continue;

            // Genuinely suspended with nothing to resolve. A pending callback means
            // the workflow is waiting on external input; the caller decides whether
            // that is a suspend point (StartAsync) or an error (RunAsync). For other
            // pending states (e.g. a real wait with SkipTime disabled) keep looping
            // until the workflow progresses, hits MaxInvocations, or times out.
            if (stopAtSuspend || HasPendingCallback(arn))
                return null;
        }
    }

    /// <summary>
    /// Runs any chained-invoke siblings started during the last invocation through
    /// the <see cref="FunctionRegistry"/> and stamps the result/error onto the
    /// stored operation so the next replay resolves it. Returns true if at least
    /// one invoke was resolved (i.e. the workflow should be re-driven).
    /// </summary>
    private async Task<bool> ResolvePendingInvokesAsync(string arn, CancellationToken cancellationToken)
    {
        if (_processor is null || _registry is null)
            return false;

        var pending = _processor.DrainPendingInvokes();
        if (pending.Count == 0)
            return false;

        foreach (var invoke in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // An unregistered sibling throws UnregisteredSiblingFunctionException
            // (out of InvokeAsync) so it surfaces with actionable guidance rather
            // than as an opaque MaxInvocations timeout.
            var (result, error) = await _registry.InvokeAsync(
                invoke.FunctionName, invoke.Payload ?? "null", _serializer, _lambdaContext);

            var op = _store.GetOperation(arn, invoke.OperationId);
            if (op is null)
                continue;

            op.ChainedInvokeDetails ??= new ChainedInvokeDetails();
            op.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (error is null)
            {
                op.Status = OperationStatuses.Succeeded;
                op.ChainedInvokeDetails.Result = result;
                op.ChainedInvokeDetails.Error = null;
            }
            else
            {
                op.Status = OperationStatuses.Failed;
                op.ChainedInvokeDetails.Error = error;
            }
            _store.Upsert(arn, op);
        }

        return true;
    }

    private bool HasPendingCallback(string arn)
    {
        foreach (var op in _store.GetAllOperations(arn))
        {
            if (op.Type == OperationTypes.Callback && op.Status == OperationStatuses.Started)
                return true;
        }
        return false;
    }

    private void SeedExecutionOperation(string arn, TInput input)
    {
        // Idempotent: re-driving (e.g. WaitForResultAsync after a callback) must
        // not reset the EXECUTION op back to Started or clobber recorded state.
        if (_store.GetOperation(arn, "exec-0") is not null)
            return;

        var serializedInput = SerializeToString(input);
        _store.Upsert(arn, new Operation
        {
            Id = "exec-0",
            Type = OperationTypes.Execution,
            Status = OperationStatuses.Started,
            ExecutionDetails = new ExecutionDetails { InputPayload = serializedInput }
        });
    }

    private DurableExecutionInvocationInput BuildInvocationInput(string arn)
    {
        return new DurableExecutionInvocationInput
        {
            DurableExecutionArn = arn,
            CheckpointToken = _store.CurrentToken(arn),
            InitialExecutionState = new InitialExecutionState
            {
                Operations = _store.GetAllOperations(arn).ToList(),
                NextMarker = null,
            }
        };
    }

    private string SerializeToString(TInput value)
    {
        using var stream = new MemoryStream();
        _serializer.Serialize(value, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private TestResult<TOutput> BuildResult(string arn, DurableExecutionInvocationOutput output, int invocationCount)
    {
        TOutput? result = default;
        if (output.Status == InvocationStatus.Succeeded && output.Result is not null)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(output.Result));
            result = _serializer.Deserialize<TOutput>(stream);
        }

        var allOps = _store.GetAllOperations(arn);
        var steps = allOps
            .Where(o => o.Type != OperationTypes.Execution)
            .Select(o => new TestStep(o, _serializer))
            .ToList();

        return new TestResult<TOutput>(
            status: output.Status,
            result: result,
            error: output.Error,
            durableExecutionArn: arn,
            invocationCount: invocationCount,
            steps: steps);
    }
}
