// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Thrown by <see cref="TestResult{TOutput}.EnsureSucceeded"/> when the workflow
/// did not complete successfully.
/// </summary>
public sealed class TestExecutionFailedException : Exception
{
    /// <summary>The final status of the workflow.</summary>
    public InvocationStatus FinalStatus { get; }

    /// <summary>The error that caused the failure, if available.</summary>
    public ErrorObject? FailureError { get; }

    /// <summary>All recorded steps at the time of failure.</summary>
    public IReadOnlyList<TestStep> Steps { get; }

    internal TestExecutionFailedException(
        InvocationStatus finalStatus,
        ErrorObject? failureError,
        IReadOnlyList<TestStep> steps)
        : base(FormatMessage(finalStatus, failureError))
    {
        FinalStatus = finalStatus;
        FailureError = failureError;
        Steps = steps;
    }

    private static string FormatMessage(InvocationStatus status, ErrorObject? error)
    {
        var msg = $"Workflow execution did not succeed. Final status: {status}.";
        if (error is not null)
        {
            msg += $" Error: [{error.ErrorType}] {error.ErrorMessage}";
        }
        return msg;
    }
}
