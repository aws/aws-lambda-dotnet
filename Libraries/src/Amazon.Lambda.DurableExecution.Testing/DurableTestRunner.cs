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
    public Task<string> StartAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        // Callback support implemented in commit 5
        throw new NotImplementedException("StartAsync will be implemented with callback support.");
    }

    /// <inheritdoc />
    public Task<string> WaitForCallbackAsync(
        string durableExecutionArn,
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("WaitForCallbackAsync will be implemented with callback support.");
    }

    /// <inheritdoc />
    public Task SendCallbackSuccessAsync<TResult>(
        string callbackId,
        TResult result,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("SendCallbackSuccessAsync will be implemented with callback support.");
    }

    /// <inheritdoc />
    public Task SendCallbackFailureAsync(
        string callbackId,
        ErrorObject? error = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("SendCallbackFailureAsync will be implemented with callback support.");
    }

    /// <inheritdoc />
    public Task SendCallbackHeartbeatAsync(
        string callbackId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("SendCallbackHeartbeatAsync will be implemented with callback support.");
    }

    /// <inheritdoc />
    public Task<TestResult<TOutput>> WaitForResultAsync(
        string durableExecutionArn,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("WaitForResultAsync will be implemented with callback support.");
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
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
