// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Thrown when a workflow calls <c>InvokeAsync</c> with a function name that has not
/// been registered via <c>RegisterFunction</c> or <c>RegisterDurableFunction</c>.
/// </summary>
public sealed class UnregisteredSiblingFunctionException : Exception
{
    /// <summary>The function name or ARN that was requested but not registered.</summary>
    public string FunctionName { get; }

    /// <summary>
    /// Creates a new instance for the unregistered function.
    /// </summary>
    public UnregisteredSiblingFunctionException(string functionName)
        : base($"No handler registered for function '{functionName}'. " +
               $"Call runner.RegisterFunction(\"{functionName}\", handler) or " +
               $"runner.RegisterDurableFunction(\"{functionName}\", handler) before running the workflow.")
    {
        FunctionName = functionName;
    }
}
