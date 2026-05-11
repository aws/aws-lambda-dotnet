using System.Diagnostics.CodeAnalysis;
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
    /// to a checkpoint using reflection-based <c>System.Text.Json</c>.
    /// For NativeAOT or trimmed deployments, use the overload that takes an
    /// <see cref="ICheckpointSerializer{T}"/>.
    /// </summary>
    [RequiresUnreferencedCode("Reflection-based JSON for T. Use the ICheckpointSerializer<T> overload for AOT/trimmed deployments.")]
    [RequiresDynamicCode("Reflection-based JSON for T. Use the ICheckpointSerializer<T> overload for AOT/trimmed deployments.")]
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
    /// Execute a step with AOT-safe checkpoint serialization. The supplied
    /// <paramref name="serializer"/> is used in place of reflection-based JSON.
    /// </summary>
    Task<T> StepAsync<T>(
        Func<IStepContext, Task<T>> func,
        ICheckpointSerializer<T> serializer,
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
