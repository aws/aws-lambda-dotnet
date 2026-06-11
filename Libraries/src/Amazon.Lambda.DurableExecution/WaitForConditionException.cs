namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Thrown when a <c>WaitForConditionAsync</c> operation reaches its
/// strategy's max-attempts limit without the condition being met.
/// </summary>
/// <remarks>
/// Designed to be subclassable: future failure modes (e.g. timeout once that's
/// implemented) should be added as derived exceptions rather than discriminator
/// flags on this type, so users can catch them by static type.
/// <see cref="LastState"/> exposes the most recently observed state so callers
/// can incorporate it into the failure path (logging, partial results, etc.).
/// </remarks>
public class WaitForConditionException : DurableExecutionException
{
    /// <summary>
    /// Number of attempts the strategy made before giving up. 1-based.
    /// </summary>
    public int AttemptsExhausted { get; init; }

    /// <summary>
    /// The most recent state observed by the check function before the
    /// strategy decided to stop. Boxed because the exception type is not
    /// generic; callers cast to the workflow's known state type.
    /// </summary>
    /// <remarks>
    /// Populated identically on live execution and on replay: the operation
    /// serializes the last observed state into the FAIL checkpoint payload,
    /// so a re-invocation that hits the cached FAIL reconstructs the same
    /// <c>LastState</c> the original execution surfaced. Will be <c>null</c>
    /// only if the FAIL checkpoint predates this serialization (legacy data)
    /// or if the serializer cannot round-trip the state.
    /// </remarks>
    public object? LastState { get; init; }

    /// <summary>Creates an empty <see cref="WaitForConditionException"/>.</summary>
    public WaitForConditionException() { }
    /// <summary>Creates a <see cref="WaitForConditionException"/> with the given message.</summary>
    public WaitForConditionException(string message) : base(message) { }
    /// <summary>Creates a <see cref="WaitForConditionException"/> wrapping an inner exception.</summary>
    public WaitForConditionException(string message, Exception innerException) : base(message, innerException) { }
}
