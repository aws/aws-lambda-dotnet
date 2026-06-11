// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Base exception for all durable execution errors.
/// </summary>
public class DurableExecutionException : Exception
{
    /// <summary>Creates an empty <see cref="DurableExecutionException"/>.</summary>
    public DurableExecutionException() { }
    /// <summary>Creates a <see cref="DurableExecutionException"/> with the given message.</summary>
    public DurableExecutionException(string message) : base(message) { }
    /// <summary>Creates a <see cref="DurableExecutionException"/> wrapping an inner exception.</summary>
    public DurableExecutionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when code has changed between invocations, causing a replay mismatch.
/// For example, a step at index 0 was previously a WAIT but is now a STEP.
/// </summary>
public class NonDeterministicExecutionException : DurableExecutionException
{
    /// <summary>Creates an empty <see cref="NonDeterministicExecutionException"/>.</summary>
    public NonDeterministicExecutionException() { }
    /// <summary>Creates a <see cref="NonDeterministicExecutionException"/> with the given message.</summary>
    public NonDeterministicExecutionException(string message) : base(message) { }
    /// <summary>Creates a <see cref="NonDeterministicExecutionException"/> wrapping an inner exception.</summary>
    public NonDeterministicExecutionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when user code inside a step fails (after retries exhausted).
/// Contains the original error details from the checkpoint.
/// </summary>
public class StepException : DurableExecutionException
{
    /// <summary>The fully-qualified type name of the original exception.</summary>
    public string? ErrorType { get; init; }
    /// <summary>Optional structured error data attached by the user.</summary>
    public string? ErrorData { get; init; }
    /// <summary>Stack trace of the original exception, captured before serialization.</summary>
    public IReadOnlyList<string>? OriginalStackTrace { get; init; }

    /// <summary>Creates an empty <see cref="StepException"/>.</summary>
    public StepException() { }
    /// <summary>Creates a <see cref="StepException"/> with the given message.</summary>
    public StepException(string message) : base(message) { }
    /// <summary>Creates a <see cref="StepException"/> wrapping an inner exception.</summary>
    public StepException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a step under <see cref="StepSemantics.AtMostOncePerRetry"/> is
/// detected to have been interrupted mid-execution on a prior invocation
/// (replay sees a <c>STARTED</c> checkpoint with no terminal record).
/// </summary>
/// <remarks>
/// Surfaces in <see cref="IRetryStrategy.ShouldRetry"/> so user-supplied
/// strategies can distinguish "my code threw" from "a previous attempt
/// crashed before it could record a result".
/// </remarks>
public class StepInterruptedException : StepException
{
    /// <summary>Creates an empty <see cref="StepInterruptedException"/>.</summary>
    public StepInterruptedException() { }
    /// <summary>Creates a <see cref="StepInterruptedException"/> with the given message.</summary>
    public StepInterruptedException(string message) : base(message) { }
    /// <summary>Creates a <see cref="StepInterruptedException"/> wrapping an inner exception.</summary>
    public StepInterruptedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a child context's user function fails. Surfaces from
/// <c>RunInChildContextAsync</c>; the underlying error is preserved on the
/// <see cref="ErrorType"/>/<see cref="ErrorData"/>/<see cref="OriginalStackTrace"/>
/// fields. Use <see cref="ChildContextConfig.ErrorMapping"/> to remap into a
/// domain-specific exception.
/// </summary>
public class ChildContextException : DurableExecutionException
{
    /// <summary>
    /// The child context's <see cref="ChildContextConfig.SubType"/>, if any.
    /// </summary>
    public string? SubType { get; init; }
    /// <summary>The fully-qualified type name of the original exception.</summary>
    public string? ErrorType { get; init; }
    /// <summary>Optional structured error data attached by the user.</summary>
    public string? ErrorData { get; init; }
    /// <summary>Stack trace of the original exception, captured before serialization.</summary>
    public IReadOnlyList<string>? OriginalStackTrace { get; init; }

    /// <summary>Creates an empty <see cref="ChildContextException"/>.</summary>
    public ChildContextException() { }
    /// <summary>Creates a <see cref="ChildContextException"/> with the given message.</summary>
    public ChildContextException(string message) : base(message) { }
    /// <summary>Creates a <see cref="ChildContextException"/> wrapping an inner exception.</summary>
    public ChildContextException(string message, Exception innerException) : base(message, innerException) { }
}
