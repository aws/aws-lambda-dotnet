// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Thrown when the workflow does not reach a terminal state within the configured
/// <see cref="TestRunnerOptions.MaxInvocations"/> limit.
/// </summary>
public sealed class TestExecutionLimitException : Exception
{
    /// <summary>The maximum invocations configured.</summary>
    public int MaxInvocations { get; }

    /// <summary>Total operations recorded at the time of the limit breach.</summary>
    public int TotalOperations { get; }

    internal TestExecutionLimitException(int maxInvocations, int totalOperations)
        : base(FormatMessage(maxInvocations, totalOperations))
    {
        MaxInvocations = maxInvocations;
        TotalOperations = totalOperations;
    }

    private static string FormatMessage(int maxInvocations, int totalOperations)
    {
        return $"""
            Workflow did not reach a terminal state within {maxInvocations} invocations.

            Possible causes:
              - Workflow uses WaitForCallbackAsync — call StartAsync/WaitForCallbackAsync/SendCallbackSuccessAsync instead of RunAsync.
              - Workflow uses InvokeAsync for a function that isn't registered — call runner.RegisterFunction("name", handler).
              - Workflow has an infinite retry loop.
              - Workflow uses WaitForConditionAsync that never returns true.

            Set TestRunnerOptions.MaxInvocations to a higher value if your workflow is legitimately long.
            Total operations recorded: {totalOperations}.
            """;
    }
}
