// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// The outcome of a durable workflow execution, including the terminal result
/// and every recorded operation for step-level inspection.
/// </summary>
public sealed class TestResult<TOutput>
{
    /// <summary>
    /// The terminal status of the workflow. The runtime's <see cref="InvocationStatus"/>
    /// has three values (Succeeded, Failed, Pending); the cloud runner maps the
    /// service's finer terminal states (FAILED, TIMED_OUT, STOPPED) onto
    /// <see cref="InvocationStatus.Failed"/>. Inspect <see cref="Error"/> for the
    /// underlying detail.
    /// </summary>
    public InvocationStatus Status { get; }

    /// <summary>True when the workflow completed successfully.</summary>
    public bool IsSucceeded => Status == InvocationStatus.Succeeded;

    /// <summary>True when the workflow reached a failed terminal state.</summary>
    public bool IsFailed => Status == InvocationStatus.Failed;

    /// <summary>
    /// The workflow result when <see cref="Status"/> is <see cref="InvocationStatus.Succeeded"/>.
    /// Default when not succeeded.
    /// </summary>
    public TOutput? Result { get; }

    /// <summary>The error when <see cref="Status"/> is <see cref="InvocationStatus.Failed"/>.</summary>
    public ErrorObject? Error { get; }

    /// <summary>The durable execution ARN for this run.</summary>
    public string DurableExecutionArn { get; }

    /// <summary>
    /// Number of handler invocations the local runner used to drive the workflow
    /// to completion. <b>null when not tracked</b> — the cloud runner never tracks
    /// it, so do not assert on <see cref="InvocationCount"/> in tests intended to
    /// run against both backends.
    /// </summary>
    public int? InvocationCount { get; }

    /// <summary>Every recorded operation except the top-level EXECUTION operation.</summary>
    public IReadOnlyList<TestStep> Steps { get; }

    internal TestResult(
        InvocationStatus status,
        TOutput? result,
        ErrorObject? error,
        string durableExecutionArn,
        int? invocationCount,
        IReadOnlyList<TestStep> steps)
    {
        Status = status;
        Result = result;
        Error = error;
        DurableExecutionArn = durableExecutionArn;
        InvocationCount = invocationCount;
        Steps = steps;

        LinkChildren();
    }

    /// <summary>
    /// Returns the first step matching <paramref name="name"/>.
    /// Throws <see cref="InvalidOperationException"/> if no match is found.
    /// </summary>
    public TestStep GetStep(string name)
    {
        return FindStep(name)
            ?? throw new InvalidOperationException(
                $"No step with name '{name}' found. Available steps: [{string.Join(", ", Steps.Where(s => s.Name is not null).Select(s => s.Name))}]");
    }

    /// <summary>
    /// Returns the first step matching <paramref name="name"/>, or null if not found.
    /// </summary>
    public TestStep? FindStep(string name)
    {
        foreach (var step in Steps)
        {
            if (string.Equals(step.Name, name, StringComparison.Ordinal))
                return step;
        }
        return null;
    }

    /// <summary>
    /// Returns all steps matching <paramref name="name"/> (e.g., parallel branches or map items).
    /// </summary>
    public IReadOnlyList<TestStep> GetSteps(string name)
    {
        var matches = new List<TestStep>();
        foreach (var step in Steps)
        {
            if (string.Equals(step.Name, name, StringComparison.Ordinal))
                matches.Add(step);
        }
        return matches;
    }

    /// <summary>
    /// Returns the step with the exact operation ID.
    /// Throws <see cref="InvalidOperationException"/> if not found.
    /// </summary>
    public TestStep GetStepById(string operationId)
    {
        foreach (var step in Steps)
        {
            if (string.Equals(step.Id, operationId, StringComparison.Ordinal))
                return step;
        }
        throw new InvalidOperationException(
            $"No step with ID '{operationId}' found.");
    }

    /// <summary>
    /// Returns all direct children of <paramref name="parent"/> (operations whose ParentId matches).
    /// </summary>
    public IReadOnlyList<TestStep> GetChildren(TestStep parent)
    {
        return parent.Children;
    }

    /// <summary>
    /// Returns all steps whose <see cref="TestStep.Status"/> equals
    /// <paramref name="status"/> (use the <see cref="OperationStatus"/> constants,
    /// e.g. <see cref="OperationStatus.Succeeded"/> or <see cref="OperationStatus.Failed"/>).
    /// </summary>
    public IReadOnlyList<TestStep> GetStepsByStatus(string status)
    {
        var matches = new List<TestStep>();
        foreach (var step in Steps)
        {
            if (string.Equals(step.Status, status, StringComparison.Ordinal))
                matches.Add(step);
        }
        return matches;
    }

    /// <summary>
    /// Throws <see cref="TestExecutionFailedException"/> if <see cref="Status"/> is not
    /// <see cref="InvocationStatus.Succeeded"/>.
    /// </summary>
    public void EnsureSucceeded()
    {
        if (Status != InvocationStatus.Succeeded)
        {
            throw new TestExecutionFailedException(Status, Error, Steps);
        }
    }

    private void LinkChildren()
    {
        var childMap = new Dictionary<string, List<TestStep>>();
        foreach (var step in Steps)
        {
            if (step.ParentId is not null)
            {
                if (!childMap.TryGetValue(step.ParentId, out var children))
                {
                    children = new List<TestStep>();
                    childMap[step.ParentId] = children;
                }
                children.Add(step);
            }
        }

        foreach (var step in Steps)
        {
            if (childMap.TryGetValue(step.Id, out var children))
            {
                step.Children = children;
            }
        }
    }
}
