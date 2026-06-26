// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Model;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Cloud test runner that invokes a real deployed durable Lambda function
/// and polls for results. Provides the same <see cref="IDurableTestRunner{TInput, TOutput}"/>
/// interface as the local runner for portable test code.
/// </summary>
public sealed class CloudDurableTestRunner<TInput, TOutput> : IDurableTestRunner<TInput, TOutput>, IAsyncDisposable
{
    private readonly string _functionArn;
    private readonly IAmazonLambda _lambdaClient;
    private readonly ILambdaSerializer _serializer;
    private readonly CloudTestRunnerOptions _options;

    /// <summary>
    /// Creates a cloud test runner targeting a deployed durable function.
    /// </summary>
    /// <param name="functionArn">Qualified function ARN (with alias, version, or $LATEST).</param>
    /// <param name="lambdaClient">AWS Lambda client. If null, creates a default client.</param>
    /// <param name="options">Cloud runner options. If null, uses defaults.</param>
    public CloudDurableTestRunner(
        string functionArn,
        IAmazonLambda? lambdaClient = null,
        CloudTestRunnerOptions? options = null)
    {
        _functionArn = functionArn ?? throw new ArgumentNullException(nameof(functionArn));
        _lambdaClient = lambdaClient ?? new AmazonLambdaClient();
        _options = options ?? new CloudTestRunnerOptions();
        _serializer = _options.Serializer ?? new DefaultLambdaJsonSerializer();
    }

    /// <inheritdoc />
    public async Task<TestResult<TOutput>> RunAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var arn = await StartAsync(input, timeout, cancellationToken);
        return await WaitForResultAsync(arn, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> StartAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? _options.DefaultTimeout);

        var payload = SerializeToString(input);

        // Fire-and-forget (Event) invocation. A synchronous (RequestResponse)
        // durable invoke blocks until the execution reaches a terminal state, so
        // a workflow that suspends on a callback would deadlock against the
        // caller — the test can only deliver the callback after StartAsync
        // returns. An Event invoke starts the execution and returns immediately;
        // we then resolve the ARN by listing executions by the name we minted.
        var executionName = $"cloud-test-{Guid.NewGuid():N}";

        await _lambdaClient.InvokeAsync(new InvokeRequest
        {
            FunctionName = _functionArn,
            InvocationType = InvocationType.Event,
            Payload = payload,
            DurableExecutionName = executionName,
        }, timeoutCts.Token);

        return await ResolveExecutionArnAsync(executionName, timeoutCts.Token);
    }

    /// <summary>
    /// Polls <c>ListDurableExecutionsByFunction</c> until the execution started
    /// with <paramref name="executionName"/> appears, returning its ARN. The
    /// listing API is eventually consistent, so the execution may not be visible
    /// on the first call after an Event invoke.
    /// </summary>
    private async Task<string> ResolveExecutionArnAsync(
        string executionName, CancellationToken cancellationToken)
    {
        // Filter by name only. The listing API rejects a request that supplies a
        // Qualifier alongside DurableExecutionName, so we pass the unqualified
        // function ARN and match the name client-side.
        var functionName = StripQualifier(_functionArn);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await _lambdaClient.ListDurableExecutionsByFunctionAsync(
                new ListDurableExecutionsByFunctionRequest
                {
                    FunctionName = functionName,
                    DurableExecutionName = executionName,
                }, cancellationToken);

            var match = response.DurableExecutions?
                .FirstOrDefault(e => e.DurableExecutionName == executionName);
            if (match?.DurableExecutionArn is { } arn)
                return arn;

            await Task.Delay(_options.PollInterval, cancellationToken);
        }
    }

    // Returns the unqualified function ARN: "arn:...:function:Name[:Qualifier]"
    // → "arn:...:function:Name". Non-ARN identifiers and already-unqualified
    // ARNs are returned unchanged.
    private static string StripQualifier(string functionArn)
    {
        const string arnPrefix = "arn:aws:lambda:";
        if (!functionArn.StartsWith(arnPrefix, StringComparison.Ordinal))
            return functionArn;

        // arn:aws:lambda:region:acct:function:name[:qualifier] — the qualifier,
        // if present, is the 8th colon-delimited field (index 7).
        var parts = functionArn.Split(':');
        return parts.Length >= 8 ? string.Join(':', parts[..7]) : functionArn;
    }

    /// <inheritdoc />
    public async Task<TestResult<TOutput>> WaitForResultAsync(
        string durableExecutionArn,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? _options.DefaultTimeout);

        // Terminal status and the workflow result/error are execution-level concepts
        // surfaced by GetDurableExecution — they are NOT on the EXECUTION operation in
        // the GetDurableExecutionState stream (that op only carries the input payload).
        while (true)
        {
            timeoutCts.Token.ThrowIfCancellationRequested();

            var execution = await _lambdaClient.GetDurableExecutionAsync(
                new GetDurableExecutionRequest { DurableExecutionArn = durableExecutionArn },
                timeoutCts.Token);

            if (IsTerminal(execution.Status))
            {
                var operations = await FetchAllOperationsAsync(durableExecutionArn, timeoutCts.Token);
                return BuildTestResult(durableExecutionArn, execution, operations);
            }

            await Task.Delay(_options.PollInterval, timeoutCts.Token);
        }
    }

    private static bool IsTerminal(ExecutionStatus? status) =>
        status == ExecutionStatus.SUCCEEDED
        || status == ExecutionStatus.FAILED
        || status == ExecutionStatus.TIMED_OUT
        || status == ExecutionStatus.STOPPED;

    /// <summary>
    /// Reconstructs the operation list from the execution history event stream.
    /// </summary>
    /// <remarks>
    /// We deliberately use <c>GetDurableExecutionHistory</c> rather than
    /// <c>GetDurableExecutionState</c>: the state API requires a
    /// <c>CheckpointToken</c> that only the Lambda runtime receives on each
    /// invocation, so an external poller (this runner) cannot call it. The
    /// history API is token-free and intended for out-of-band observers. We fold
    /// the per-operation lifecycle events (Started → Succeeded/Failed/…) back into
    /// the same <see cref="Operation"/> shape the rest of this class expects, so
    /// <see cref="TestStep"/> inspection behaves identically to the local runner.
    /// </remarks>
    private async Task<List<Operation>> FetchAllOperationsAsync(
        string durableExecutionArn, CancellationToken cancellationToken)
    {
        // Preserve first-seen order so step lists read top-to-bottom like the
        // workflow body, then fold each event into its operation by Id.
        var operations = new Dictionary<string, Operation>(StringComparer.Ordinal);
        var order = new List<string>();
        string? marker = null;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _lambdaClient.GetDurableExecutionHistoryAsync(
                new GetDurableExecutionHistoryRequest
                {
                    DurableExecutionArn = durableExecutionArn,
                    IncludeExecutionData = true,
                    Marker = marker,
                }, cancellationToken);

            foreach (var evt in response.Events ?? new List<Event>())
            {
                if (evt.Id is not { } id) continue;

                if (!operations.TryGetValue(id, out var op))
                {
                    op = new Operation { Id = id };
                    operations[id] = op;
                    order.Add(id);
                }

                ApplyEvent(op, evt);
            }

            marker = string.IsNullOrEmpty(response.NextMarker) ? null : response.NextMarker;
        }
        while (marker is not null);

        return order.Select(id => operations[id]).ToList();
    }

    /// <summary>
    /// Folds a single history <see cref="Event"/> into the running
    /// <see cref="Operation"/> it belongs to. Started events seed the type,
    /// name, and start time; terminal events set the status, end time, and the
    /// type-specific result/error payload.
    /// </summary>
    private static void ApplyEvent(Operation op, Event evt)
    {
        // Name / ParentId / SubType ride on every event for the operation; the
        // later events repeat them, so last-write-wins is fine.
        if (!string.IsNullOrEmpty(evt.Name)) op.Name = evt.Name;
        if (!string.IsNullOrEmpty(evt.ParentId)) op.ParentId = evt.ParentId;
        if (!string.IsNullOrEmpty(evt.SubType)) op.SubType = evt.SubType;

        var ts = evt.EventTimestamp.HasValue
            ? new DateTimeOffset(evt.EventTimestamp.Value, TimeSpan.Zero).ToUnixTimeMilliseconds()
            : (long?)null;

        var type = evt.EventType;

        // Step
        if (type == EventType.StepStarted)
        {
            op.Type = OperationTypes.Step;
            op.Status = OperationStatuses.Started;
            op.StartTimestamp ??= ts;
        }
        else if (type == EventType.StepSucceeded)
        {
            op.Type = OperationTypes.Step;
            op.Status = OperationStatuses.Succeeded;
            op.EndTimestamp = ts;
            op.StepDetails ??= new StepDetails();
            op.StepDetails.Result = evt.StepSucceededDetails?.Result?.Payload;
            if (evt.StepSucceededDetails?.RetryDetails?.CurrentAttempt is { } attempt)
                op.StepDetails.Attempt = attempt;
        }
        else if (type == EventType.StepFailed)
        {
            op.Type = OperationTypes.Step;
            op.Status = OperationStatuses.Failed;
            op.EndTimestamp = ts;
            op.StepDetails ??= new StepDetails();
            op.StepDetails.Error = MapEventError(evt.StepFailedDetails?.Error);
            if (evt.StepFailedDetails?.RetryDetails?.CurrentAttempt is { } attempt)
                op.StepDetails.Attempt = attempt;
        }
        // Wait
        else if (type == EventType.WaitStarted)
        {
            op.Type = OperationTypes.Wait;
            op.Status = OperationStatuses.Started;
            op.StartTimestamp ??= ts;
            if (evt.WaitStartedDetails?.ScheduledEndTimestamp is { } end)
                op.WaitDetails = new WaitDetails
                {
                    ScheduledEndTimestamp = new DateTimeOffset(end, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                };
        }
        else if (type == EventType.WaitSucceeded)
        {
            op.Type = OperationTypes.Wait;
            op.Status = OperationStatuses.Succeeded;
            op.EndTimestamp = ts;
        }
        else if (type == EventType.WaitCancelled)
        {
            op.Type = OperationTypes.Wait;
            op.Status = OperationStatuses.Cancelled;
            op.EndTimestamp = ts;
        }
        // Callback
        else if (type == EventType.CallbackStarted)
        {
            op.Type = OperationTypes.Callback;
            op.Status = OperationStatuses.Started;
            op.StartTimestamp ??= ts;
            op.CallbackDetails ??= new CallbackDetails();
            op.CallbackDetails.CallbackId = evt.CallbackStartedDetails?.CallbackId;
        }
        else if (type == EventType.CallbackSucceeded)
        {
            op.Type = OperationTypes.Callback;
            op.Status = OperationStatuses.Succeeded;
            op.EndTimestamp = ts;
            op.CallbackDetails ??= new CallbackDetails();
            op.CallbackDetails.Result = evt.CallbackSucceededDetails?.Result?.Payload;
        }
        else if (type == EventType.CallbackFailed)
        {
            op.Type = OperationTypes.Callback;
            op.Status = OperationStatuses.Failed;
            op.EndTimestamp = ts;
            op.CallbackDetails ??= new CallbackDetails();
            op.CallbackDetails.Error = MapEventError(evt.CallbackFailedDetails?.Error);
        }
        else if (type == EventType.CallbackTimedOut)
        {
            op.Type = OperationTypes.Callback;
            op.Status = OperationStatuses.TimedOut;
            op.EndTimestamp = ts;
        }
        // Chained invoke
        else if (type == EventType.ChainedInvokeStarted)
        {
            op.Type = OperationTypes.ChainedInvoke;
            op.Status = OperationStatuses.Started;
            op.StartTimestamp ??= ts;
        }
        else if (type == EventType.ChainedInvokeSucceeded)
        {
            op.Type = OperationTypes.ChainedInvoke;
            op.Status = OperationStatuses.Succeeded;
            op.EndTimestamp = ts;
            op.ChainedInvokeDetails ??= new ChainedInvokeDetails();
            op.ChainedInvokeDetails.Result = evt.ChainedInvokeSucceededDetails?.Result?.Payload;
        }
        else if (type == EventType.ChainedInvokeFailed)
        {
            op.Type = OperationTypes.ChainedInvoke;
            op.Status = OperationStatuses.Failed;
            op.EndTimestamp = ts;
            op.ChainedInvokeDetails ??= new ChainedInvokeDetails();
            op.ChainedInvokeDetails.Error = MapEventError(evt.ChainedInvokeFailedDetails?.Error);
        }
        // Child context
        else if (type == EventType.ContextStarted)
        {
            op.Type = OperationTypes.Context;
            op.Status = OperationStatuses.Started;
            op.StartTimestamp ??= ts;
        }
        else if (type == EventType.ContextSucceeded)
        {
            op.Type = OperationTypes.Context;
            op.Status = OperationStatuses.Succeeded;
            op.EndTimestamp = ts;
            op.ContextDetails ??= new ContextDetails();
            op.ContextDetails.Result = evt.ContextSucceededDetails?.Result?.Payload;
        }
        else if (type == EventType.ContextFailed)
        {
            op.Type = OperationTypes.Context;
            op.Status = OperationStatuses.Failed;
            op.EndTimestamp = ts;
            op.ContextDetails ??= new ContextDetails();
            op.ContextDetails.Error = MapEventError(evt.ContextFailedDetails?.Error);
        }
        // Execution-level events: keep the type so they're filtered out of the
        // step list by BuildTestResult; the workflow result/error come from
        // GetDurableExecution, not from here.
        else if (type == EventType.ExecutionStarted
                 || type == EventType.ExecutionSucceeded
                 || type == EventType.ExecutionFailed
                 || type == EventType.ExecutionTimedOut
                 || type == EventType.ExecutionStopped)
        {
            op.Type = OperationTypes.Execution;
        }
    }

    private static ErrorObject? MapEventError(EventError? eventError)
    {
        var sdkError = eventError?.Payload;
        if (sdkError is null) return null;
        return new ErrorObject
        {
            ErrorType = sdkError.ErrorType,
            ErrorMessage = sdkError.ErrorMessage,
            ErrorData = sdkError.ErrorData,
            StackTrace = sdkError.StackTrace,
        };
    }

    /// <inheritdoc />
    public async Task<string> WaitForCallbackAsync(
        string durableExecutionArn,
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? _options.DefaultTimeout);

        while (true)
        {
            timeoutCts.Token.ThrowIfCancellationRequested();

            // Reconstruct from history (token-free) — see FetchAllOperationsAsync.
            var operations = await FetchAllOperationsAsync(durableExecutionArn, timeoutCts.Token);

            foreach (var op in operations)
            {
                if (op.Type == OperationTypes.Callback
                    && op.Status == OperationStatuses.Started
                    && op.CallbackDetails?.CallbackId is { } cbId)
                {
                    if (name is null || MatchesCallbackName(op.Name, name))
                        return cbId;
                }
            }

            await Task.Delay(_options.PollInterval, timeoutCts.Token);
        }
    }

    /// <inheritdoc />
    public async Task SendCallbackSuccessAsync<TResult>(
        string callbackId,
        TResult result,
        CancellationToken cancellationToken = default)
    {
        var serialized = SerializeToString(result);
        await _lambdaClient.SendDurableExecutionCallbackSuccessAsync(
            new SendDurableExecutionCallbackSuccessRequest
            {
                CallbackId = callbackId,
                Result = new MemoryStream(Encoding.UTF8.GetBytes(serialized)),
            }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendCallbackFailureAsync(
        string callbackId,
        ErrorObject? error = null,
        CancellationToken cancellationToken = default)
    {
        await _lambdaClient.SendDurableExecutionCallbackFailureAsync(
            new SendDurableExecutionCallbackFailureRequest
            {
                CallbackId = callbackId,
                Error = error is not null ? new Amazon.Lambda.Model.ErrorObject
                {
                    ErrorType = error.ErrorType,
                    ErrorMessage = error.ErrorMessage,
                    ErrorData = error.ErrorData,
                    StackTrace = error.StackTrace?.ToList(),
                } : null,
            }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendCallbackHeartbeatAsync(
        string callbackId,
        CancellationToken cancellationToken = default)
    {
        await _lambdaClient.SendDurableExecutionCallbackHeartbeatAsync(
            new SendDurableExecutionCallbackHeartbeatRequest
            {
                CallbackId = callbackId,
            }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private TestResult<TOutput> BuildTestResult(
        string arn, GetDurableExecutionResponse execution, IReadOnlyList<Operation> allOps)
    {
        var status = MapExecutionStatus(execution.Status);

        TOutput? result = default;
        if (status == InvocationStatus.Succeeded && !string.IsNullOrEmpty(execution.Result))
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(execution.Result));
            result = _serializer.Deserialize<TOutput>(stream);
        }

        var steps = allOps
            .Where(o => o.Type != OperationTypes.Execution)
            .Select(o => new TestStep(o, _serializer))
            .ToList();

        return new TestResult<TOutput>(
            status: status,
            result: result,
            error: MapSdkError(execution.Error),
            durableExecutionArn: arn,
            invocationCount: -1,
            steps: steps);
    }

    // The runtime's terminal states beyond Succeeded (FAILED/TIMED_OUT/STOPPED)
    // all map to Failed since InvocationStatus has no finer terminal distinction.
    private static InvocationStatus MapExecutionStatus(ExecutionStatus? status)
    {
        if (status == ExecutionStatus.SUCCEEDED) return InvocationStatus.Succeeded;
        if (status == ExecutionStatus.FAILED
            || status == ExecutionStatus.TIMED_OUT
            || status == ExecutionStatus.STOPPED) return InvocationStatus.Failed;
        return InvocationStatus.Pending;
    }

    private static ErrorObject? MapSdkError(Amazon.Lambda.Model.ErrorObject? sdkError)
    {
        if (sdkError is null) return null;
        return new ErrorObject
        {
            ErrorType = sdkError.ErrorType,
            ErrorMessage = sdkError.ErrorMessage,
            ErrorData = sdkError.ErrorData,
            StackTrace = sdkError.StackTrace,
        };
    }

    private static bool MatchesCallbackName(string? opName, string name)
    {
        if (opName is null) return false;
        if (string.Equals(opName, name, StringComparison.Ordinal)) return true;
        if (string.Equals(opName, $"{name}-callback", StringComparison.Ordinal)) return true;
        return false;
    }

    private string SerializeToString<T>(T value)
    {
        using var stream = new MemoryStream();
        _serializer.Serialize(value, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
