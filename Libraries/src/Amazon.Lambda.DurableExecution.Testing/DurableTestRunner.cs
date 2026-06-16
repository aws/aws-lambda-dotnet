// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Local test runner for durable workflows. Drives the workflow to completion
/// in-process using the real runtime engine with an in-memory service backend.
/// </summary>
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
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options ?? new TestRunnerOptions();
        _serializer = _options.Serializer ?? new DefaultLambdaJsonSerializer();
        _lambdaContext = CreateLambdaContext();
        _store = new InMemoryOperationStore();
        _processor = new CheckpointProcessor(_store, _options.SkipTime);
        _serviceClient = new InMemoryDurableServiceClient(_store, _processor);
        _registry = new FunctionRegistry();
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

        var orchestrator = _lastOrchestrator ?? CreateOrchestrator();
        var result = await orchestrator.DriveToTerminalAsync(
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
            _handler, _store, _serviceClient, _lambdaContext, _options, _serializer);
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
