// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Serializable error representation stored in checkpoint state.
/// </summary>
public sealed class ErrorObject
{
    /// <summary>
    /// The fully-qualified exception type name.
    /// </summary>
    [JsonPropertyName("ErrorType")]
    public string? ErrorType { get; set; }

    /// <summary>
    /// The exception message.
    /// </summary>
    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace frames.
    /// </summary>
    [JsonPropertyName("StackTrace")]
    public IReadOnlyList<string>? StackTrace { get; set; }

    /// <summary>
    /// Additional serialized error data.
    /// </summary>
    [JsonPropertyName("ErrorData")]
    public string? ErrorData { get; set; }

    /// <summary>
    /// Creates an ErrorObject from an exception.
    /// </summary>
    /// <remarks>
    /// SDK operation wrappers (<see cref="StepException"/>,
    /// <see cref="ChildContextException"/>, <see cref="InvokeException"/>,
    /// <see cref="CallbackException"/>) unwrap to the original error captured
    /// from the failed operation — preserving the user-visible
    /// <c>ErrorType</c>/<c>ErrorData</c>/<c>StackTrace</c> instead of recording
    /// the wrapper's type. This way a chained invoker sees the originating
    /// exception (e.g. <c>System.InvalidOperationException</c>) rather than
    /// <c>Amazon.Lambda.DurableExecution.StepException</c>. Mirrors the Java
    /// SDK's <c>DurableExecutor.buildErrorObject</c> behavior.
    /// </remarks>
    public static ErrorObject FromException(Exception exception)
    {
        return exception switch
        {
            StepException step => new ErrorObject
            {
                ErrorType = step.ErrorType,
                ErrorMessage = step.Message,
                StackTrace = step.OriginalStackTrace,
                ErrorData = step.ErrorData
            },
            ChildContextException child => new ErrorObject
            {
                ErrorType = child.ErrorType,
                ErrorMessage = child.Message,
                StackTrace = child.OriginalStackTrace,
                ErrorData = child.ErrorData
            },
            InvokeException invoke => new ErrorObject
            {
                ErrorType = invoke.ErrorType,
                ErrorMessage = invoke.Message,
                StackTrace = invoke.OriginalStackTrace,
                ErrorData = invoke.ErrorData
            },
            CallbackException callback => new ErrorObject
            {
                ErrorType = callback.ErrorType,
                ErrorMessage = callback.Message,
                StackTrace = callback.OriginalStackTrace,
                ErrorData = callback.ErrorData
            },
            _ => new ErrorObject
            {
                ErrorType = exception.GetType().FullName,
                ErrorMessage = exception.Message,
                StackTrace = exception.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            }
        };
    }
}
