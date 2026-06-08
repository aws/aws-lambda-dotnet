using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Context passed to a <c>WaitForConditionAsync</c> check function on every
/// polling iteration. Provides a logger scoped to the current attempt and the
/// 1-based attempt number, mirroring the surface of
/// <see cref="IStepContext"/> (minus <c>OperationId</c>: every iteration of a
/// wait-for-condition operation shares the same operation ID, so exposing it
/// here would be misleading — see <c>DESIGN-QUESTIONS.md#Q6</c>).
/// </summary>
public interface IConditionCheckContext
{
    /// <summary>
    /// Logger scoped to this condition-check attempt.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// The current 1-based attempt number. Increments on every polling
    /// iteration; on replay, equals the number of attempts already
    /// checkpointed plus one.
    /// </summary>
    int AttemptNumber { get; }
}
