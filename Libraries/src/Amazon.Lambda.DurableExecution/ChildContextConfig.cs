// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for a child context.
/// </summary>
/// <remarks>
/// A child context is a logical sub-workflow with its own deterministic
/// operation-ID space, persisted as a <c>CONTEXT</c> operation. Use
/// <see cref="IDurableContext.RunInChildContextAsync{T}(System.Func{IDurableContext, System.Threading.Tasks.Task{T}}, string?, ChildContextConfig?, System.Threading.CancellationToken)"/>
/// (and overloads) to run code inside one.
/// </remarks>
public sealed class ChildContextConfig
{
    /// <summary>
    /// Operation sub-type label for observability (e.g. <c>"WaitForCallback"</c>).
    /// Surfaces on the wire <c>OperationUpdate.SubType</c> field.
    /// </summary>
    public string? SubType { get; set; }

    /// <summary>
    /// Optional function to transform exceptions thrown by the child context's
    /// user function before they surface to the caller. Useful for wrapping
    /// low-level errors into domain-specific exceptions.
    /// </summary>
    /// <remarks>
    /// Applied when the user function throws (the mapped exception propagates
    /// to the caller of <c>RunInChildContextAsync</c>) and on replay of a
    /// <c>FAILED</c> child context (the constructed
    /// <see cref="ChildContextException"/> is mapped before being thrown).
    /// </remarks>
    public Func<Exception, Exception>? ErrorMapping { get; set; }
}
