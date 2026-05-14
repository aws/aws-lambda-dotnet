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
