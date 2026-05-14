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
    /// A logger scoped to the durable execution. Currently returns
    /// <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance"/>;
    /// the replay-safe <c>DurableLogger</c> (suppresses messages during replay)
    /// ships in a follow-up PR.
    /// </summary>
    ILogger Logger { get; }

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
    Task<T> StepAsync<T>(
        Func<IStepContext, Task<T>> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a step that returns no value.
    /// </summary>
    Task StepAsync(
        Func<IStepContext, Task> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspend execution for the specified duration without consuming compute time.
    /// The Lambda is suspended and the service re-invokes it after the wait elapses.
    /// Duration must be at least 1 second (service timer granularity).
    /// </summary>
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
    Task<T> RunInChildContextAsync<T>(
        Func<IDurableContext, Task<T>> func,
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
    Task RunInChildContextAsync(
        Func<IDurableContext, Task> func,
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
    Task<ICallback<T>> CreateCallbackAsync<T>(
        string? name = null,
        CallbackConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Composite operation that creates a callback, runs the supplied submitter
    /// (which hands the <c>callbackId</c> to an external system), and suspends
    /// until the external system delivers a result. Equivalent to manually
    /// composing <see cref="CreateCallbackAsync{T}(string?, CallbackConfig?, System.Threading.CancellationToken)"/>
    /// + <see cref="StepAsync{T}(System.Func{IStepContext, System.Threading.Tasks.Task{T}}, string?, StepConfig?, System.Threading.CancellationToken)"/>
    /// + <see cref="ICallback{T}.GetResultAsync(System.Threading.CancellationToken)"/>
    /// inside a child context.
    /// </summary>
    /// <remarks>
    /// Submitter failures (after retries are exhausted) surface as
    /// <see cref="CallbackSubmitterException"/>. Callback failures and timeouts
    /// surface as <see cref="CallbackFailedException"/> /
    /// <see cref="CallbackTimeoutException"/>.
    /// </remarks>
    Task<T> WaitForCallbackAsync<T>(
        Func<string, IWaitForCallbackContext, Task> submitter,
        string? name = null,
        WaitForCallbackConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoke another durable Lambda function and await its result. The
    /// invocation is checkpointed so it survives parent failures and is not
    /// double-fired on replay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The chained function runs out-of-process: the SDK checkpoints a
    /// <c>CHAINED_INVOKE START</c> with the supplied <paramref name="payload"/>
    /// and suspends; the durable execution service runs the target function
    /// asynchronously and re-invokes the parent workflow when it completes.
    /// On resume, the cached result is deserialized and returned, or the
    /// recorded failure surfaces as an <see cref="InvokeException"/> subclass.
    /// </para>
    /// <para>
    /// <paramref name="functionName"/> must be a qualified identifier (version,
    /// alias, or <c>$LATEST</c>). Unqualified ARNs are rejected by the durable
    /// execution service. The SDK only validates that the value is non-null
    /// and non-empty; ARN shape validation is left to the service to keep
    /// behavior consistent with the Python, JavaScript, and Java SDKs.
    /// </para>
    /// <para>
    /// The payload and result are serialized to/from a checkpoint using the
    /// <see cref="ILambdaSerializer"/> registered on
    /// <see cref="ILambdaContext.Serializer"/> (typically configured via
    /// <c>LambdaBootstrapBuilder.Create(handler, serializer)</c>). AOT and
    /// reflection-based scenarios share this single overload — the AOT story
    /// is determined by the registered serializer (e.g.,
    /// <c>SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;</c>).
    /// </para>
    /// </remarks>
    /// <typeparam name="TPayload">The payload type sent to the chained function.</typeparam>
    /// <typeparam name="TResult">The result type returned by the chained function.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="functionName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="functionName"/> is empty or whitespace.</exception>
    /// <exception cref="InvokeFailedException">The chained function threw.</exception>
    /// <exception cref="InvokeTimedOutException">The chained function did not complete within the configured timeout.</exception>
    /// <exception cref="InvokeStoppedException">The chained execution was stopped by the service.</exception>
    Task<TResult> InvokeAsync<TPayload, TResult>(
        string functionName,
        TPayload payload,
        string? name = null,
        InvokeConfig? config = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to step functions.
/// </summary>
public interface IStepContext
{
    /// <summary>
    /// Logger scoped to this step.
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
