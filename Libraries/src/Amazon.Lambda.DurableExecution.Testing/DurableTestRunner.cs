// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.LocalEmulation;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Local test runner for durable workflows. Drives the workflow to completion
/// in-process using the real runtime engine with an in-memory service backend.
/// </summary>
/// <remarks>
/// A runner instance is single-use and not thread-safe: it tracks the last
/// started execution in instance state (<c>_lastOrchestrator</c>,
/// <c>_lastStartInput</c>, <c>_completedResults</c>, <c>_consumedCallbackIds</c>),
/// so a single workflow should be driven per instance and the methods should not
/// be called concurrently. Create a new runner for each workflow under test.
/// </remarks>
public sealed class DurableTestRunner<TInput, TOutput> : IDurableTestRunner<TInput, TOutput>, IAsyncDisposable
{
    private readonly Func<TInput, IDurableContext, Task<TOutput>> _handler;
    private readonly TestRunnerOptions _options;
    private readonly ILambdaSerializer _serializer;
    private readonly ILambdaContext _lambdaContext;
    private readonly InMemoryOperationStore _store;
    private readonly CheckpointProcessor _processor;
    private readonly InMemoryDurableServiceClient _serviceClient;
    private readonly FunctionRegistry _registry;
    private readonly Dictionary<string, TestResult<TOutput>> _completedResults = new();
    private readonly HashSet<string> _consumedCallbackIds = new();
    private ExecutionOrchestrator<TInput, TOutput>? _lastOrchestrator;
    private TInput? _lastStartInput;

    /// <summary>
    /// Creates a new local test runner for the given workflow handler.
    /// </summary>
    public DurableTestRunner(
        Func<TInput, IDurableContext, Task<TOutput>> handler,
        TestRunnerOptions? options = null)
        : this(handler, options, registry: null)
    {
    }

    /// <summary>
    /// Creates a local test runner that shares an existing <see cref="FunctionRegistry"/>.
    /// Used when a durable sibling is invoked: the nested runner inherits the parent's
    /// registered functions so chains of durable-to-durable invokes resolve.
    /// </summary>
    internal DurableTestRunner(
        Func<TInput, IDurableContext, Task<TOutput>> handler,
        TestRunnerOptions? options,
        FunctionRegistry? registry)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options ?? new TestRunnerOptions();
        _serializer = _options.Serializer ?? new DefaultLambdaJsonSerializer();
        _lambdaContext = CreateLambdaContext();
        _store = new InMemoryOperationStore();
        _processor = new CheckpointProcessor(_store, _options.SkipTime);
        _serviceClient = new InMemoryDurableServiceClient(_store, _processor);
        _registry = registry ?? new FunctionRegistry(_options);
    }

    /// <summary>
    /// Registers a plain (non-durable) Lambda handler as a sibling function.
    /// </summary>
    public DurableTestRunner<TInput, TOutput> RegisterFunction<TPayload, TResult>(
        string functionNameOrArn,
        Func<TPayload, ILambdaContext, Task<TResult>> handler)
    {
        _registry.RegisterPlain(functionNameOrArn, handler);
        return this;
    }

    /// <summary>
    /// Registers a durable Lambda handler as a sibling function.
    /// </summary>
    public DurableTestRunner<TInput, TOutput> RegisterDurableFunction<TPayload, TResult>(
        string functionNameOrArn,
        Func<TPayload, IDurableContext, Task<TResult>> handler)
    {
        _registry.RegisterDurable(functionNameOrArn, handler);
        return this;
    }

    /// <inheritdoc />
    public async Task<TestResult<TOutput>> RunAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var orchestrator = CreateOrchestrator();
        return await orchestrator.DriveToTerminalAsync(
            _options.DurableExecutionArn,
            input,
            timeout ?? _options.DefaultTimeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> StartAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var arn = _options.DurableExecutionArn;
        var orchestrator = CreateOrchestrator();
        _lastStartInput = input;
        _lastOrchestrator = orchestrator;

        var result = await orchestrator.DriveUntilSuspendedAsync(
            arn, input, timeout ?? _options.DefaultTimeout, cancellationToken);

        if (result is not null)
            _completedResults[arn] = result;

        return arn;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Unlike the cloud runner, this performs a single synchronous scan rather
    /// than polling. The local runner is single-threaded and <see cref="StartAsync"/>
    /// drives the workflow to its suspension point before it returns, so any pending
    /// callback is already recorded in the in-memory store by the time this is called.
    /// Nothing external mutates the store between calls, so a poll/retry loop would
    /// only re-scan unchanging state; the immediate throw surfaces the misuse faster.
    /// </remarks>
    public Task<string> WaitForCallbackAsync(
        string durableExecutionArn,
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var ops = _store.GetAllOperations(durableExecutionArn);
        foreach (var op in ops)
        {
            if (op.Type == OperationTypes.Callback
                && op.Status == OperationStatuses.Started
                && op.CallbackDetails?.CallbackId is { } cbId)
            {
                if (name is null || MatchesCallbackName(op.Name, name))
                {
                    if (!_consumedCallbackIds.Contains(cbId))
                    {
                        _consumedCallbackIds.Add(cbId);
                        return Task.FromResult(cbId);
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"No pending callback found{(name is not null ? $" with name '{name}'" : "")} for execution '{durableExecutionArn}'. " +
            "Ensure the workflow has reached a WaitForCallbackAsync point before calling this method.");
    }

    private static bool MatchesCallbackName(string? opName, string name)
    {
        if (opName is null) return false;
        // Exact match
        if (string.Equals(opName, name, StringComparison.Ordinal)) return true;
        // The runtime names inner callback ops as "{name}-callback"
        if (string.Equals(opName, $"{name}-callback", StringComparison.Ordinal)) return true;
        return false;
    }

    /// <inheritdoc />
    public Task SendCallbackSuccessAsync<TResult>(
        string callbackId,
        TResult result,
        CancellationToken cancellationToken = default)
    {
        var (arn, op) = FindCallbackOperation(callbackId);
        var serialized = SerializeToString(result);

        op.Status = OperationStatuses.Succeeded;
        op.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        op.CallbackDetails!.Result = serialized;
        _store.Upsert(arn, op);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendCallbackFailureAsync(
        string callbackId,
        ErrorObject? error = null,
        CancellationToken cancellationToken = default)
    {
        var (arn, op) = FindCallbackOperation(callbackId);

        op.Status = OperationStatuses.Failed;
        op.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        op.CallbackDetails!.Error = error;
        _store.Upsert(arn, op);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendCallbackHeartbeatAsync(
        string callbackId,
        CancellationToken cancellationToken = default)
    {
        // Heartbeats are a no-op for local testing — just validate the callback exists
        FindCallbackOperation(callbackId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<TestResult<TOutput>> WaitForResultAsync(
        string durableExecutionArn,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (_completedResults.TryGetValue(durableExecutionArn, out var cached))
            return cached;

        // Without a prior StartAsync there is no orchestrator or captured input to
        // resume from. Falling back to a fresh orchestrator would silently drive the
        // workflow with default(TInput) (null for reference types, zero-value structs
        // for value types) and produce a confusing result — throw instead.
        if (_lastOrchestrator is null)
            throw new InvalidOperationException(
                $"WaitForResultAsync was called for execution '{durableExecutionArn}' without a prior StartAsync. " +
                "Call StartAsync first to begin the workflow, then WaitForResultAsync to drive it to completion.");

        var result = await _lastOrchestrator.DriveToTerminalAsync(
            durableExecutionArn,
            _lastStartInput!,
            timeout ?? _options.DefaultTimeout,
            cancellationToken);

        _completedResults[durableExecutionArn] = result;
        return result;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // No-op: the in-memory store, processor, and orchestrator hold no
        // unmanaged or disposable resources (no timers, sockets, or clients).
        // Disposal exists to satisfy the IAsyncDisposable contract and to give
        // callers a uniform `await using` pattern across both runners. Revisit
        // if any of those collaborators ever acquire resources.
        return ValueTask.CompletedTask;
    }

    private (string Arn, Operation Op) FindCallbackOperation(string callbackId)
    {
        var arn = _options.DurableExecutionArn;
        var ops = _store.GetAllOperations(arn);
        foreach (var op in ops)
        {
            if (op.Type == OperationTypes.Callback
                && op.CallbackDetails?.CallbackId == callbackId)
            {
                return (arn, op);
            }
        }
        throw new InvalidOperationException(
            $"No callback operation found with ID '{callbackId}'.");
    }

    private string? SerializeToString<T>(T value)
    {
        if (value is null) return null;
        using var stream = new MemoryStream();
        _serializer.Serialize(value, stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private ExecutionOrchestrator<TInput, TOutput> CreateOrchestrator()
    {
        return new ExecutionOrchestrator<TInput, TOutput>(
            _handler, _store, _serviceClient, _lambdaContext, _options, _serializer,
            _processor, _registry);
    }

    private TestLambdaContext CreateLambdaContext()
    {
        var ctx = new TestLambdaContext
        {
            FunctionName = "test-durable-function",
            FunctionVersion = "$LATEST",
            RemainingTime = TimeSpan.FromMinutes(15),
        };
        ctx.Serializer = _serializer;
        return ctx;
    }
}
