// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Classifies a <see cref="TestStep"/> by the underlying operation type.
/// </summary>
public enum OperationKind
{
    /// <summary>A step operation (user function call).</summary>
    Step,

    /// <summary>A wait/timer operation.</summary>
    Wait,

    /// <summary>A callback operation (external signal).</summary>
    Callback,

    /// <summary>A chained-invoke operation (durable-to-durable or durable-to-plain call).</summary>
    ChainedInvoke,

    /// <summary>A context operation (child context, parallel, map).</summary>
    Context,

    /// <summary>The top-level execution operation.</summary>
    Execution
}
