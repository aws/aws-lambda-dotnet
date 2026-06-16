// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Common interface for local and cloud durable test runners. Tests written
/// against this interface can run unchanged against either backend.
/// </summary>
public interface IDurableTestRunner<TInput, TOutput>
{
    /// <summary>
    /// Drives the workflow to a terminal state and returns the result.
    /// Throws if the workflow requires callbacks — use the two-call pattern instead.
    /// </summary>
    Task<TestResult<TOutput>> RunAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the workflow and returns the durable execution ARN.
    /// Use with <see cref="WaitForCallbackAsync"/> for callback workflows.
    /// </summary>
    Task<string> StartAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Blocks until the workflow reaches a callback point and returns the callback ID.
    /// </summary>
    Task<string> WaitForCallbackAsync(
        string durableExecutionArn,
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a success result to a waiting callback.
    /// </summary>
    Task SendCallbackSuccessAsync<TResult>(
        string callbackId,
        TResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a failure to a waiting callback.
    /// </summary>
    Task SendCallbackFailureAsync(
        string callbackId,
        ErrorObject? error = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a heartbeat to keep a callback alive.
    /// </summary>
    Task SendCallbackHeartbeatAsync(
        string callbackId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the workflow to reach a terminal state and returns the result.
    /// </summary>
    Task<TestResult<TOutput>> WaitForResultAsync(
        string durableExecutionArn,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
