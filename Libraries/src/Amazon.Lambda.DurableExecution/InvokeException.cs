namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Thrown when a chained invoke operation reaches a non-success terminal state.
/// </summary>
/// <remarks>
/// Base class for the invoke exception tree. Catch <see cref="InvokeException"/>
/// to handle every chained-invoke failure mode uniformly, or pattern-match the
/// concrete subclasses to react differently to specific outcomes:
/// <list type="bullet">
///   <item><see cref="InvokeFailedException"/> — the chained function threw.</item>
///   <item><see cref="InvokeTimedOutException"/> — the configured (or service)
///       timeout elapsed before completion.</item>
///   <item><see cref="InvokeStoppedException"/> — the chained execution was
///       stopped by the service or an operator.</item>
/// </list>
/// Mirrors the Java SDK's <c>InvokeException</c> / <c>InvokeFailedException</c>
/// / <c>InvokeTimedOutException</c> / <c>InvokeStoppedException</c> tree; the
/// .NET SDK keeps <see cref="InvokeException"/> non-abstract so callers can also
/// rethrow it directly when wrapping fallback logic.
/// </remarks>
public class InvokeException : DurableExecutionException
{
    /// <summary>The fully-qualified name of the invoked function (ARN, alias, or version).</summary>
    public string? FunctionName { get; init; }

    /// <summary>The fully-qualified type name of the original exception, when known.</summary>
    public string? ErrorType { get; init; }

    /// <summary>Optional structured error data attached by the invoked function.</summary>
    public string? ErrorData { get; init; }

    /// <summary>Stack trace of the original exception, captured before serialization.</summary>
    public IReadOnlyList<string>? OriginalStackTrace { get; init; }

    /// <summary>Creates an empty <see cref="InvokeException"/>.</summary>
    public InvokeException() { }
    /// <summary>Creates an <see cref="InvokeException"/> with the given message.</summary>
    public InvokeException(string message) : base(message) { }
    /// <summary>Creates an <see cref="InvokeException"/> wrapping an inner exception.</summary>
    public InvokeException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a chained invoke operation completes with status <c>FAILED</c> —
/// the invoked function ran and threw.
/// </summary>
public class InvokeFailedException : InvokeException
{
    /// <summary>Creates an empty <see cref="InvokeFailedException"/>.</summary>
    public InvokeFailedException() { }
    /// <summary>Creates an <see cref="InvokeFailedException"/> with the given message.</summary>
    public InvokeFailedException(string message) : base(message) { }
    /// <summary>Creates an <see cref="InvokeFailedException"/> wrapping an inner exception.</summary>
    public InvokeFailedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a chained invoke operation completes with status <c>TIMED_OUT</c>
/// — the invocation did not complete within the service-level timeout.
/// </summary>
public class InvokeTimedOutException : InvokeException
{
    /// <summary>Creates an empty <see cref="InvokeTimedOutException"/>.</summary>
    public InvokeTimedOutException() { }
    /// <summary>Creates an <see cref="InvokeTimedOutException"/> with the given message.</summary>
    public InvokeTimedOutException(string message) : base(message) { }
    /// <summary>Creates an <see cref="InvokeTimedOutException"/> wrapping an inner exception.</summary>
    public InvokeTimedOutException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a chained invoke operation completes with status <c>STOPPED</c>
/// — the invocation was stopped administratively by the durable execution
/// service before reaching a normal terminal state.
/// </summary>
public class InvokeStoppedException : InvokeException
{
    /// <summary>Creates an empty <see cref="InvokeStoppedException"/>.</summary>
    public InvokeStoppedException() { }
    /// <summary>Creates an <see cref="InvokeStoppedException"/> with the given message.</summary>
    public InvokeStoppedException(string message) : base(message) { }
    /// <summary>Creates an <see cref="InvokeStoppedException"/> wrapping an inner exception.</summary>
    public InvokeStoppedException(string message, Exception innerException) : base(message, innerException) { }
}
