// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// The primary interface for durable execution operations.
/// Passed to user workflow functions to access checkpointed steps and waits.
/// Additional operations (callbacks, parallel, map, etc.) are added in
/// follow-up PRs.
/// </summary>
public interface IDurableContext
{
    /// <summary>
    /// Replay-safe logger. Messages emitted while the workflow is re-deriving
    /// prior operations from checkpointed state are suppressed by default, so
    /// a 30-step workflow re-invoked 30 times still emits each line once.
    /// Use this instead of <c>Console.WriteLine</c> or other ambient loggers,
    /// which will repeat on every replay. Replace the underlying logger or
    /// disable replay-aware filtering via <see cref="ConfigureLogger"/>.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Swap the underlying logger or toggle replay-aware filtering. Idempotent —
    /// later calls overwrite earlier configuration.
    /// </summary>
    /// <param name="config">
    /// The logger configuration specifying the underlying logger and whether
    /// replay-aware filtering is enabled.
    /// </param>
    void ConfigureLogger(LoggerConfig config);

    /// <summary>
    /// Metadata about the current durable execution.
    /// </summary>
    IExecutionContext ExecutionContext { get; }

    /// <summary>
    /// The underlying Lambda context.
    /// </summary>
    ILambdaContext LambdaContext { get; }

    /// <summary>
    /// Execute a step with automatic checkpointing. The step result is serialized
    /// to a checkpoint using the <see cref="ILambdaSerializer"/> registered on
    /// <see cref="ILambdaContext.Serializer"/>. AOT and reflection-based scenarios
    /// share this single overload — the AOT story is determined by the registered
    /// serializer (e.g., <c>SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;</c>).
    /// </summary>
    /// <typeparam name="T">The type of the step's result.</typeparam>
    /// <param name="func">
    /// The step body to execute. Receives an <see cref="IStepContext"/> exposing
    /// the step's logger, attempt number, and operation ID, and a
    /// <see cref="CancellationToken"/> linking the caller-supplied token with
    /// the SDK's workflow-shutdown signal — pass it to cancellation-aware APIs
    /// (<c>HttpClient.SendAsync</c>, <c>Task.Delay</c>, AWS SDK calls) so the
    /// step body unwinds cleanly when the workflow is being torn down.
    /// </param>
    /// <param name="name">
    /// An optional name for the step, used for observability and to derive the
    /// deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="config">
    /// Optional step configuration (e.g. retry policy). Defaults are used when null.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation. Linked with an SDK-owned workflow
    /// shutdown source; the resulting token is forwarded to <paramref name="func"/>.
    /// </param>
    /// <returns>The deserialized result of the step.</returns>
    Task<T> StepAsync<T>(
        Func<IStepContext, CancellationToken, Task<T>> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a step that returns no value.
    /// </summary>
    /// <param name="func">
    /// The step body to execute. Receives an <see cref="IStepContext"/> exposing
    /// the step's logger, attempt number, and operation ID, and a
    /// <see cref="CancellationToken"/> linking the caller-supplied token with
    /// the SDK's workflow-shutdown signal.
    /// </param>
    /// <param name="name">
    /// An optional name for the step, used for observability and to derive the
    /// deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="config">
    /// Optional step configuration (e.g. retry policy). Defaults are used when null.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation. Linked with an SDK-owned workflow
    /// shutdown source; the resulting token is forwarded to <paramref name="func"/>.
    /// </param>
    Task StepAsync(
        Func<IStepContext, CancellationToken, Task> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspend execution for the specified duration without consuming compute time.
    /// The Lambda is suspended and the service re-invokes it after the wait elapses.
    /// Duration must be at least 1 second (service timer granularity).
    /// </summary>
    /// <param name="duration">
    /// How long to suspend execution. Must be at least 1 second.
    /// </param>
    /// <param name="name">
    /// An optional name for the wait, used for observability and to derive the
    /// deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task WaitAsync(
        TimeSpan duration,
        string? name = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run a user function inside a logical sub-workflow (a "child context").
    /// The child has its own deterministic operation-ID space; its result is
    /// checkpointed as a <c>CONTEXT</c> operation so subsequent invocations
    /// replay the cached value without re-executing the func.
    /// </summary>
    /// <remarks>
    /// Use child contexts to group related durable operations (e.g. a step plus
    /// a wait plus a step) into a single observability/error-handling boundary.
    /// On failure, surfaces as <see cref="ChildContextException"/>; supply
    /// <see cref="ChildContextConfig.ErrorMapping"/> to remap into a
    /// domain-specific exception.
    /// The child context's return value is serialized to a checkpoint using the
    /// <see cref="ILambdaSerializer"/> registered on
    /// <see cref="ILambdaContext.Serializer"/>.
    /// </remarks>
    /// <typeparam name="T">The type of the child context's result.</typeparam>
    /// <param name="func">
    /// The user function to run inside the child context. Receives a nested
    /// <see cref="IDurableContext"/> with its own deterministic operation-ID space,
    /// and a <see cref="CancellationToken"/> linking the caller-supplied token with
    /// the SDK's workflow-shutdown signal.
    /// </param>
    /// <param name="name">
    /// An optional name for the child context, used for observability and to derive
    /// the deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="config">
    /// Optional child context configuration (e.g.
    /// <see cref="ChildContextConfig.ErrorMapping"/>). Defaults are used when null.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation. Linked with an SDK-owned workflow
    /// shutdown source; the resulting token is forwarded to <paramref name="func"/>.
    /// </param>
    /// <returns>The deserialized result of the child context.</returns>
    Task<T> RunInChildContextAsync<T>(
        Func<IDurableContext, CancellationToken, Task<T>> func,
        string? name = null,
        ChildContextConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run a user function inside a logical sub-workflow (a "child context")
    /// that returns no value. The child has its own deterministic operation-ID
    /// space and is checkpointed as a <c>CONTEXT</c> operation so subsequent
    /// invocations skip re-executing the func.
    /// </summary>
    /// <remarks>
    /// Use child contexts to group related durable operations (e.g. a step plus
    /// a wait plus a step) into a single observability/error-handling boundary.
    /// On failure, surfaces as <see cref="ChildContextException"/>; supply
    /// <see cref="ChildContextConfig.ErrorMapping"/> to remap into a
    /// domain-specific exception.
    /// </remarks>
    /// <param name="func">
    /// The user function to run inside the child context. Receives a nested
    /// <see cref="IDurableContext"/> with its own deterministic operation-ID space,
    /// and a <see cref="CancellationToken"/> linking the caller-supplied token with
    /// the SDK's workflow-shutdown signal.
    /// </param>
    /// <param name="name">
    /// An optional name for the child context, used for observability and to derive
    /// the deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="config">
    /// Optional child context configuration (e.g.
    /// <see cref="ChildContextConfig.ErrorMapping"/>). Defaults are used when null.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation. Linked with an SDK-owned workflow
    /// shutdown source; the resulting token is forwarded to <paramref name="func"/>.
    /// </param>
    Task RunInChildContextAsync(
        Func<IDurableContext, CancellationToken, Task> func,
        string? name = null,
        ChildContextConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a callback for an external system to complete. Returns an
    /// <see cref="ICallback{T}"/> handle exposing the service-allocated
    /// <see cref="ICallback{T}.CallbackId"/> (pass to the external system) and
    /// <see cref="ICallback{T}.GetResultAsync(System.Threading.CancellationToken)"/>
    /// (await to suspend until a result arrives).
    /// </summary>
    /// <remarks>
    /// The callback result is deserialized using the <see cref="ILambdaSerializer"/>
    /// registered on <see cref="ILambdaContext.Serializer"/>. AOT and reflection-based
    /// scenarios share this single overload — the AOT story is determined by the
    /// registered serializer (e.g.,
    /// <c>SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;</c>).
    /// <para>
    /// Errors are deferred to <see cref="ICallback{T}.GetResultAsync(System.Threading.CancellationToken)"/>;
    /// <c>CreateCallbackAsync</c> always returns successfully so user code
    /// between <c>CreateCallbackAsync</c> and the result-await runs deterministically
    /// across replays.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type of the result the callback will deliver.</typeparam>
    /// <param name="name">
    /// An optional name for the callback, used for observability and to derive the
    /// deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="config">
    /// Optional callback configuration (e.g. timeout). Defaults are used when null.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// An <see cref="ICallback{T}"/> handle exposing the service-allocated callback
    /// ID and a method to await the result.
    /// </returns>
    Task<ICallback<T>> CreateCallbackAsync<T>(
        string? name = null,
        CallbackConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Composite operation that creates a callback, runs the supplied submitter
    /// (which hands the <c>callbackId</c> to an external system), and suspends
    /// until the external system delivers a result. Equivalent to manually
    /// composing <see cref="CreateCallbackAsync{T}(string?, CallbackConfig?, System.Threading.CancellationToken)"/>
    /// + <see cref="StepAsync{T}(System.Func{IStepContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}, string?, StepConfig?, System.Threading.CancellationToken)"/>
    /// + <see cref="ICallback{T}.GetResultAsync(System.Threading.CancellationToken)"/>
    /// inside a child context.
    /// </summary>
    /// <remarks>
    /// Submitter failures (after retries are exhausted) surface as
    /// <see cref="CallbackSubmitterException"/>. Callback failures and timeouts
    /// surface as <see cref="CallbackFailedException"/> /
    /// <see cref="CallbackTimeoutException"/>.
    /// </remarks>
    /// <typeparam name="T">The type of the result the callback will deliver.</typeparam>
    /// <param name="submitter">
    /// A function that hands the service-allocated <c>callbackId</c> to the external
    /// system. Receives the callback ID, an <see cref="IWaitForCallbackContext"/>,
    /// and a <see cref="CancellationToken"/> linking the caller-supplied token with
    /// the SDK's workflow-shutdown signal.
    /// </param>
    /// <param name="name">
    /// An optional name for the operation, used for observability and to derive the
    /// deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="config">
    /// Optional configuration (e.g. submitter retry policy and callback timeout).
    /// Defaults are used when null.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation. Linked with an SDK-owned workflow
    /// shutdown source; the resulting token is forwarded to <paramref name="submitter"/>.
    /// </param>
    /// <returns>The deserialized result delivered by the external system.</returns>
    Task<T> WaitForCallbackAsync<T>(
        Func<string, IWaitForCallbackContext, CancellationToken, Task> submitter,
        string? name = null,
        WaitForCallbackConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoke another durable Lambda function and await its result. The
    /// invocation is checkpointed so it survives parent failures and is not
    /// double-fired on replay. The payload and result are serialized to/from
    /// a checkpoint using the <see cref="ILambdaSerializer"/> registered on
    /// <see cref="ILambdaContext.Serializer"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="functionName"/> must be a qualified identifier (version,
    /// alias, or <c>$LATEST</c>); unqualified ARNs are rejected by the durable
    /// execution service.
    /// </remarks>
    /// <typeparam name="TPayload">The type of the payload sent to the target function.</typeparam>
    /// <typeparam name="TResult">The type of the result returned by the target function.</typeparam>
    /// <param name="functionName">
    /// The qualified identifier (version, alias, or <c>$LATEST</c>) of the durable
    /// Lambda function to invoke. Unqualified ARNs are rejected.
    /// </param>
    /// <param name="payload">The payload to pass to the target function.</param>
    /// <param name="name">
    /// An optional name for the invocation, used for observability and to derive the
    /// deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="config">
    /// Optional invocation configuration (e.g. retry policy). Defaults are used when null.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The deserialized result returned by the target function.</returns>
    Task<TResult> InvokeAsync<TPayload, TResult>(
        string functionName,
        TPayload payload,
        string? name = null,
        InvokeConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Poll a condition by repeatedly invoking <paramref name="check"/> until
    /// the configured <see cref="IWaitStrategy{TState}"/> decides to stop.
    /// Between polls the workflow is suspended (no compute charge); the
    /// service re-invokes the Lambda when the strategy's chosen delay elapses.
    /// </summary>
    /// <remarks>
    /// On every iteration the <paramref name="check"/> function receives the
    /// state returned by the previous invocation (seeded by
    /// <see cref="WaitForConditionConfig{TState}.InitialState"/> on the very
    /// first call), so users can carry per-poll bookkeeping (e.g. a cursor or
    /// retry counter) inside the state itself. If the strategy stops because
    /// of <see cref="IWaitStrategy{TState}"/>'s max-attempts limit (rather
    /// than because the condition is met), a <see cref="WaitForConditionException"/>
    /// is thrown carrying the last observed state.
    /// The check function's return value is serialized to a checkpoint using
    /// the <see cref="ILambdaSerializer"/> registered on
    /// <see cref="ILambdaContext.Serializer"/>. AOT and reflection-based
    /// scenarios share this single overload — the AOT story is determined by
    /// the registered serializer (e.g.,
    /// <c>SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;</c>).
    /// </remarks>
    /// <typeparam name="TState">
    /// The type of the per-poll state carried between condition checks.
    /// </typeparam>
    /// <param name="check">
    /// The condition check invoked on each poll. Receives the state returned by the
    /// previous invocation (seeded by
    /// <see cref="WaitForConditionConfig{TState}.InitialState"/> on the first call),
    /// an <see cref="IConditionCheckContext"/>, and a <see cref="CancellationToken"/>
    /// linking the caller-supplied token with the SDK's workflow-shutdown signal,
    /// and returns the next state.
    /// </param>
    /// <param name="config">
    /// The configuration controlling polling, including the
    /// <see cref="IWaitStrategy{TState}"/> and the initial state.
    /// </param>
    /// <param name="name">
    /// An optional name for the operation, used for observability and to derive the
    /// deterministic operation ID. Defaults to a name inferred from the call site.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation. Linked with an SDK-owned workflow
    /// shutdown source; the resulting token is forwarded to <paramref name="check"/>.
    /// </param>
    /// <returns>The final state observed when the strategy decides to stop.</returns>
    Task<TState> WaitForConditionAsync<TState>(
        Func<TState, IConditionCheckContext, CancellationToken, Task<TState>> check,
        WaitForConditionConfig<TState> config,
        string? name = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to step functions.
/// </summary>
public interface IStepContext
{
    /// <summary>
    /// Logger scoped to this step. Same instance as
    /// <see cref="IDurableContext.Logger"/>; emits within an
    /// <see cref="ILogger.BeginScope{TState}"/> that carries the step's
    /// <c>operationId</c>, <c>operationName</c>, and <c>attempt</c>.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// The current retry attempt number (1-based).
    /// </summary>
    int AttemptNumber { get; }

    /// <summary>
    /// The deterministic operation ID for this step.
    /// </summary>
    string OperationId { get; }
}

/// <summary>
/// Metadata about the current execution.
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// The ARN of the current durable execution.
    /// </summary>
    string DurableExecutionArn { get; }
}
