// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.LocalEmulation;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;

namespace Amazon.Lambda.TestTool.Services.DurableExecution;

/// <summary>
/// Plays the role of the durable-execution service's control plane for a locally-running
/// function: starts executions and drives them to a terminal state by repeatedly invoking the
/// function over the Lambda Runtime API and interpreting each <c>Pending</c>/terminal response.
/// </summary>
/// <remarks>
/// This is the out-of-process analogue of <c>ExecutionOrchestrator</c> in the testing package.
/// The essential difference: the orchestrator calls the workflow via an in-process C# delegate,
/// whereas this driver invokes the developer's real, separately-running function by queuing an
/// event into its <see cref="IRuntimeApiDataStore"/> (RequestResponse mode) and awaiting the
/// response the function posts back through the Runtime API. The invocation envelope
/// (<see cref="DurableExecutionInvocationInput"/>) and result
/// (<see cref="DurableExecutionInvocationOutput"/>) are plain serializable POCOs, which is what
/// makes the transport swap possible.
///
/// Supports step / wait / retry / child-context / parallel / map, external callbacks (the loop
/// parks on a pending callback and resumes when it's resolved), and chained durable invokes
/// (each sibling runs as its own nested durable execution whose result is stamped back onto the
/// parent). Executions can be stopped via <see cref="Stop"/>.
/// </remarks>
internal sealed class DurableExecutionDriver
{
    private static readonly JsonSerializerOptions EnvelopeJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly DurableExecutionStore _store;
    private readonly IRuntimeApiDataStoreManager _runtimeApiDataStoreManager;
    private readonly int _maxInvocations;

    // Guards start-time idempotency and tracks in-flight/finished executions.
    private readonly ConcurrentDictionary<string, ExecutionRecord> _executions = new();
    // Idempotency: (functionName, executionName) -> ARN of the already-started execution.
    private readonly ConcurrentDictionary<string, StartedExecution> _byName = new();

    public DurableExecutionDriver(
        DurableExecutionStore store,
        IRuntimeApiDataStoreManager runtimeApiDataStoreManager,
        int maxInvocations = 10_000)
    {
        _store = store;
        _runtimeApiDataStoreManager = runtimeApiDataStoreManager;
        _maxInvocations = maxInvocations;
    }

    /// <summary>Terminal/transient state of a single execution, observable by callers/tests.</summary>
    internal sealed class ExecutionRecord
    {
        public required string Arn { get; init; }
        public required string FunctionName { get; init; }
        public volatile ExecutionPhase Phase = ExecutionPhase.Running;
        public string? Result;
        public ErrorObject? Error;
        public string? FailureReason;
        public readonly TaskCompletionSource<ExecutionRecord> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Signals the drive loop to wake from a callback park. Re-created each time the loop
        // parks so a resolve that arrives while running (before the next park) is not lost —
        // ResumeRequested records that case. Guarded by ResumeGate so park re-arm and notify
        // cannot race into a lost wakeup.
        public readonly object ResumeGate = new();
        public TaskCompletionSource ResumeSignal =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ResumeRequested;

        // Cancels the drive loop when the execution is stopped (StopDurableExecution).
        public readonly CancellationTokenSource Cts = new();
    }

    internal enum ExecutionPhase { Running, ParkedOnCallback, Succeeded, Failed, Stopped }

    private readonly record struct StartedExecution(string Arn, string PayloadHash);

    /// <summary>
    /// Starts (or idempotently re-attaches to) a durable execution and launches its drive loop on
    /// a background task. Returns the execution ARN immediately, mirroring the service's async
    /// start (the caller gets a 202 + <c>X-Amz-Durable-Execution-Arn</c>).
    /// </summary>
    /// <exception cref="DurableExecutionAlreadyStartedException">
    /// The same execution name was already started with a different payload.
    /// </exception>
    public string Start(string functionName, string? executionName, string payload)
    {
        var name = string.IsNullOrEmpty(executionName) ? Guid.NewGuid().ToString() : executionName;
        var payloadHash = ComputeHash(payload);
        var nameKey = $"{functionName}{name}";

        // Idempotency: same name + same payload returns the existing ARN; a differing payload is
        // an error — matching the service's DurableExecutionAlreadyStartedException contract.
        var started = _byName.GetOrAdd(nameKey, _ =>
        {
            var arn = _store.MintArn(functionName, name);
            return new StartedExecution(arn, payloadHash);
        });

        if (started.PayloadHash != payloadHash)
        {
            throw new DurableExecutionAlreadyStartedException(
                $"A durable execution named '{name}' for function '{functionName}' was already started with a different payload.");
        }

        // Launch the drive loop exactly once per ARN.
        var launched = false;
        _executions.GetOrAdd(started.Arn, arn =>
        {
            _store.SeedExecution(arn, payload);
            var record = new ExecutionRecord { Arn = arn, FunctionName = functionName };
            _ = Task.Run(() => DriveAsync(record));
            launched = true;
            return record;
        });

        if (launched)
            RaiseStateChange();

        return started.Arn;
    }

    /// <summary>Awaits the execution's terminal state. Used by tests and (later) synchronous callers.</summary>
    public Task<ExecutionRecord> WaitForCompletionAsync(string arn)
        => _executions.TryGetValue(arn, out var record)
            ? record.Completion.Task
            : Task.FromException<ExecutionRecord>(
                new InvalidOperationException($"No durable execution found for ARN '{arn}'."));

    /// <summary>Current record for an ARN, if the execution is known.</summary>
    public ExecutionRecord? TryGetExecution(string arn)
        => _executions.TryGetValue(arn, out var record) ? record : null;

    /// <summary>All known executions, newest activity first isn't guaranteed; UI sorts as needed.</summary>
    public IReadOnlyCollection<ExecutionRecord> GetExecutions() => _executions.Values.ToList();

    /// <summary>Snapshot of the operations recorded for an execution (for the UI timeline).</summary>
    public IReadOnlyList<Operation> GetOperations(string arn) => _store.GetAllOperations(arn);

    /// <summary>
    /// Whether timers/retry backoff are time-skipped (resolved immediately) rather than waiting
    /// for real wall-clock time. Toggleable at runtime from the web UI; affects subsequent
    /// checkpoints across all executions.
    /// </summary>
    public bool SkipTime
    {
        get => _store.SkipTime;
        set => _store.SkipTime = value;
    }

    /// <summary>
    /// Resolves a pending callback and wakes the parked drive loop. Used by the web UI's
    /// "Send Callback" action (the same effect as an external SendDurableExecutionCallback* call,
    /// without an HTTP round-trip). Returns the resolution outcome.
    /// </summary>
    public DurableExecutionStore.CallbackResolution SendCallback(string callbackId, string? result, ErrorObject? error)
    {
        var (outcome, arn) = _store.ResolveCallback(callbackId, result, error);
        if (outcome == DurableExecutionStore.CallbackResolution.Resolved && arn is not null)
            NotifyCallbackResolved(arn);
        return outcome;
    }

    /// <summary>Raised whenever an execution starts or changes phase, so the UI can refresh.</summary>
    public event EventHandler? StateChange;

    private void RaiseStateChange() => StateChange?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Wakes the drive loop for an execution whose callback was just resolved (via the callback
    /// endpoints). Safe to call whether the loop is currently parked or still running — if it
    /// hasn't parked yet, <c>ResumeRequested</c> ensures the next park is skipped rather than
    /// blocking on an already-satisfied condition.
    /// </summary>
    public void NotifyCallbackResolved(string arn)
    {
        if (!_executions.TryGetValue(arn, out var record))
            return;

        lock (record.ResumeGate)
        {
            record.ResumeRequested = true;
            record.ResumeSignal.TrySetResult();
        }
        RaiseStateChange();
    }

    /// <summary>
    /// Stops a running durable execution (StopDurableExecution): cancels the drive loop, marks the
    /// EXECUTION operation STOPPED, and completes the execution as Stopped. Returns false if the
    /// ARN is unknown or the execution already reached a terminal state.
    /// </summary>
    public bool Stop(string arn)
    {
        if (!_executions.TryGetValue(arn, out var record))
            return false;
        if (record.Phase is ExecutionPhase.Succeeded or ExecutionPhase.Failed or ExecutionPhase.Stopped)
            return false;

        record.Cts.Cancel();
        // Wake the loop if it's parked on a callback so it observes the cancellation promptly.
        lock (record.ResumeGate)
        {
            record.ResumeRequested = true;
            record.ResumeSignal.TrySetResult();
        }
        _store.MarkStopped(arn);
        record.Phase = ExecutionPhase.Stopped;
        record.Completion.TrySetResult(record);
        RaiseStateChange();
        return true;
    }

    private async Task DriveAsync(ExecutionRecord record)
    {
        var arn = record.Arn;
        var runtimeStore = _runtimeApiDataStoreManager.GetLambdaRuntimeDataStore(record.FunctionName);
        var invocationCount = 0;
        var ct = record.Cts.Token;

        try
        {
            while (true)
            {
                if (ct.IsCancellationRequested)
                    return; // Stopped — Stop() has already set the terminal state.

                if (invocationCount >= _maxInvocations)
                {
                    Fail(record, $"Execution exceeded the maximum of {_maxInvocations} invocations without completing. " +
                                 "This usually means the workflow is not making progress (a non-deterministic workflow, or a wait/retry loop).");
                    return;
                }

                var envelopeJson = BuildInvocationEnvelopeJson(arn);

                // Invoke the running function over the Runtime API and wait for it to post back.
                // EventContainer.WaitForCompletion blocks up to 15 minutes and ignores
                // cancellation, so poll its status against the drive-loop token instead — this
                // lets Stop() (and test teardown) abandon a still-running invocation promptly
                // rather than pinning a threadpool thread until the fail-safe elapses.
                var evnt = runtimeStore.QueueEvent(envelopeJson, isRequestResponseMode: true);
                try
                {
                    while (evnt.EventStatus is EventContainer.Status.Queued or EventContainer.Status.Executing)
                        await Task.Delay(50, ct);
                }
                catch (OperationCanceledException)
                {
                    evnt.Dispose();
                    return; // Stopped/torn down mid-invocation.
                }

                if (evnt.EventStatus == EventContainer.Status.Failure)
                {
                    // The function crashed (an unhandled host error, not a durable FAILED result).
                    Fail(record, $"Function invocation failed: {evnt.ErrorType}: {evnt.ErrorResponse}");
                    evnt.Dispose();
                    return;
                }

                var output = ParseOutput(evnt.Response);
                evnt.Dispose();
                invocationCount++;

                if (output is null)
                {
                    Fail(record, "Function returned an empty or unparseable durable-execution output envelope.");
                    return;
                }

                if (output.Status == InvocationStatus.Succeeded)
                {
                    record.Result = output.Result;
                    record.Phase = ExecutionPhase.Succeeded;
                    record.Completion.TrySetResult(record);
                    RaiseStateChange();
                    return;
                }

                if (output.Status == InvocationStatus.Failed)
                {
                    record.Error = output.Error;
                    record.Phase = ExecutionPhase.Failed;
                    record.Completion.TrySetResult(record);
                    RaiseStateChange();
                    return;
                }

                // Pending. A chained-invoke START suspends the workflow expecting the service to run
                // a sibling durable function. Resolve each by starting a nested durable execution
                // against the sibling and stamping its result/error onto the parent's op, then
                // re-drive so replay sees them resolved.
                var pendingInvokes = _store.DrainPendingInvokes(arn);
                if (pendingInvokes.Count > 0)
                {
                    await ResolveChainedInvokesAsync(arn, pendingInvokes);
                    continue;
                }

                // A pending callback means the workflow is waiting on external input. Park until a
                // SendDurableExecutionCallback* request resolves it (via NotifyCallbackResolved),
                // then re-invoke so replay sees the resolved callback. A resolve that raced ahead
                // of the park is captured by ResumeRequested so it is never missed.
                if (HasPendingCallback(arn))
                {
                    Task resumeTask;
                    lock (record.ResumeGate)
                    {
                        if (record.ResumeRequested)
                        {
                            // A resolve raced ahead of the park — consume it and re-invoke now.
                            record.ResumeRequested = false;
                            record.ResumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                            continue;
                        }
                        record.Phase = ExecutionPhase.ParkedOnCallback;
                        resumeTask = record.ResumeSignal.Task;
                    }

                    RaiseStateChange();
                    await resumeTask;

                    lock (record.ResumeGate)
                    {
                        record.ResumeRequested = false;
                        record.ResumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        record.Phase = ExecutionPhase.Running;
                    }
                    continue;
                }

                // Otherwise the workflow is pending on a timer / retry backoff. With time-skipping
                // on (the default) waits/retries are already folded to ready in the store, so we
                // re-drive immediately. With time-skipping off, delay until the next scheduled
                // resume so the loop doesn't spin the CPU and hit the invocation cap.
                if (DurableEmulationHelpers.TryGetNextResumeDelay(_store.GetAllOperations(arn), out var delay) && delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay);
                }

                // Stamp any now-elapsed WAITs as SUCCEEDED (mirroring the service firing the timer)
                // before the next replay, so the store and UI timeline show the wait completed
                // rather than a perpetual STARTED. No-op when time-skip already resolved them.
                _store.CompleteElapsedWaits(arn);
            }
        }
        catch (Exception ex)
        {
            Fail(record, $"Unexpected error while driving durable execution: {ex}");
        }
    }

    /// <summary>
    /// Resolves chained durable invokes by running each sibling as its own nested durable
    /// execution and stamping the outcome onto the parent's CHAINED_INVOKE operation. The sibling
    /// runs the same drive loop (its own function must be polling the Runtime API), so nested
    /// waits/callbacks/steps all work. A sibling that never runs surfaces via its own invocation cap.
    /// </summary>
    private async Task ResolveChainedInvokesAsync(string parentArn, IReadOnlyList<CheckpointProcessor.PendingInvoke> pending)
    {
        foreach (var invoke in pending)
        {
            var siblingFunction = ExtractFunctionName(invoke.FunctionName);

            // Start the sibling as a nested durable execution and await its terminal state. Each
            // chained invoke gets a fresh execution name so retries/replays don't collide.
            var siblingArn = Start(siblingFunction, executionName: null, payload: invoke.Payload ?? "null");
            var sibling = await WaitForCompletionAsync(siblingArn);

            if (sibling.Phase == ExecutionPhase.Succeeded)
            {
                _store.ResolveChainedInvoke(parentArn, invoke.OperationId, sibling.Result, error: null);
            }
            else
            {
                var error = sibling.Error ?? new ErrorObject
                {
                    ErrorType = "ChainedInvokeFailed",
                    ErrorMessage = sibling.FailureReason ?? $"Chained invoke of '{siblingFunction}' failed."
                };
                _store.ResolveChainedInvoke(parentArn, invoke.OperationId, result: null, error: error);
            }
        }
    }

    /// <summary>
    /// Reduces a chained-invoke target (a bare name or a qualified/unqualified function ARN) to the
    /// function-name partition the Runtime API is keyed by. For an ARN, that's the segment after
    /// <c>function:</c> (dropping any version/alias qualifier); otherwise the value as-is.
    /// </summary>
    internal static string ExtractFunctionName(string functionNameOrArn)
    {
        const string marker = "function:";
        var idx = functionNameOrArn.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return functionNameOrArn;

        var afterMarker = functionNameOrArn[(idx + marker.Length)..];
        // Strip a trailing :qualifier (version or alias) if present.
        var colon = afterMarker.IndexOf(':');
        return colon >= 0 ? afterMarker[..colon] : afterMarker;
    }

    private void Fail(ExecutionRecord record, string reason)
    {
        record.FailureReason = reason;
        record.Error ??= new ErrorObject
        {
            ErrorType = "DurableExecutionEmulatorError",
            ErrorMessage = reason
        };
        record.Phase = ExecutionPhase.Failed;
        record.Completion.TrySetResult(record);
        RaiseStateChange();
    }

    private string BuildInvocationEnvelopeJson(string arn)
    {
        // Send a bounded first page of history in the envelope and let the function page the rest
        // via GetDurableExecutionState (the NextMarker), honoring the service's payload caps
        // rather than inlining an unbounded history into a single queued event.
        var (page, nextMarker) = _store.GetState(arn, marker: null);

        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = arn,
            CheckpointToken = _store.CurrentToken(arn),
            InitialExecutionState = new InitialExecutionState
            {
                Operations = page.ToList(),
                NextMarker = nextMarker
            }
        };

        return JsonSerializer.Serialize(input, EnvelopeJsonOptions);
    }

    private static DurableExecutionInvocationOutput? ParseOutput(string? responseJson)
    {
        if (string.IsNullOrEmpty(responseJson))
            return null;
        try
        {
            return JsonSerializer.Deserialize<DurableExecutionInvocationOutput>(responseJson, EnvelopeJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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

    private static string ComputeHash(string payload)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}

/// <summary>
/// Thrown when a durable execution is started with a name that already exists but a different
/// payload. Mirrors the service's <c>DurableExecutionAlreadyStartedException</c>.
/// </summary>
internal sealed class DurableExecutionAlreadyStartedException : Exception
{
    public DurableExecutionAlreadyStartedException(string message) : base(message) { }
}
