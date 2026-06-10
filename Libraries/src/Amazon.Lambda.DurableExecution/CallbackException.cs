// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Base exception type for callback failures surfaced from
/// <see cref="ICallback{T}.GetResultAsync(System.Threading.CancellationToken)"/>
/// or
/// <see cref="IDurableContext.WaitForCallbackAsync{T}(System.Func{string, IWaitForCallbackContext, System.Threading.CancellationToken, System.Threading.Tasks.Task}, string?, WaitForCallbackConfig?, System.Threading.CancellationToken)"/>.
/// Concrete subclasses distinguish failure modes — pattern-match
/// <see cref="CallbackFailedException"/>, <see cref="CallbackTimeoutException"/>,
/// or <see cref="CallbackSubmitterException"/> in <c>catch</c> clauses.
/// </summary>
public class CallbackException : DurableExecutionException
{
    /// <summary>The callback ID associated with the failure (if known).</summary>
    public string? CallbackId { get; init; }

    /// <summary>The fully-qualified type name of the original error, if known.</summary>
    public string? ErrorType { get; init; }

    /// <summary>Optional structured error data attached by the external system.</summary>
    public string? ErrorData { get; init; }

    /// <summary>Stack trace of the original error, captured before serialization.</summary>
    public IReadOnlyList<string>? OriginalStackTrace { get; init; }

    /// <summary>Creates an empty <see cref="CallbackException"/>.</summary>
    public CallbackException() { }

    /// <summary>Creates a <see cref="CallbackException"/> with the given message.</summary>
    public CallbackException(string message) : base(message) { }

    /// <summary>Creates a <see cref="CallbackException"/> wrapping an inner exception.</summary>
    public CallbackException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the external system reports a failure result for a callback
/// (via <c>SendDurableExecutionCallbackFailure</c>).
/// </summary>
public class CallbackFailedException : CallbackException
{
    /// <summary>Creates an empty <see cref="CallbackFailedException"/>.</summary>
    public CallbackFailedException() { }

    /// <summary>Creates a <see cref="CallbackFailedException"/> with the given message.</summary>
    public CallbackFailedException(string message) : base(message) { }

    /// <summary>Creates a <see cref="CallbackFailedException"/> wrapping an inner exception.</summary>
    public CallbackFailedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when the durable execution service marks a callback as timed-out —
/// either the overall <see cref="CallbackConfig.Timeout"/> or the
/// <see cref="CallbackConfig.HeartbeatTimeout"/> elapsed.
/// </summary>
public class CallbackTimeoutException : CallbackException
{
    /// <summary>Creates an empty <see cref="CallbackTimeoutException"/>.</summary>
    public CallbackTimeoutException() { }

    /// <summary>Creates a <see cref="CallbackTimeoutException"/> with the given message.</summary>
    public CallbackTimeoutException(string message) : base(message) { }

    /// <summary>Creates a <see cref="CallbackTimeoutException"/> wrapping an inner exception.</summary>
    public CallbackTimeoutException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown only from
/// <see cref="IDurableContext.WaitForCallbackAsync{T}(System.Func{string, IWaitForCallbackContext, System.Threading.CancellationToken, System.Threading.Tasks.Task}, string?, WaitForCallbackConfig?, System.Threading.CancellationToken)"/>
/// when the user-supplied submitter delegate (the step that hands the callback
/// ID to the external system) fails after retries are exhausted. Wraps the
/// underlying <see cref="StepException"/> as <see cref="System.Exception.InnerException"/>.
/// </summary>
public class CallbackSubmitterException : CallbackException
{
    /// <summary>Creates an empty <see cref="CallbackSubmitterException"/>.</summary>
    public CallbackSubmitterException() { }

    /// <summary>Creates a <see cref="CallbackSubmitterException"/> with the given message.</summary>
    public CallbackSubmitterException(string message) : base(message) { }

    /// <summary>Creates a <see cref="CallbackSubmitterException"/> wrapping an inner exception.</summary>
    public CallbackSubmitterException(string message, Exception innerException) : base(message, innerException) { }
}
