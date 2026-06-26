// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// A single operation recorded during workflow execution, exposed for test assertions.
/// Wraps the internal <see cref="Operation"/> with typed accessors.
/// </summary>
public sealed class TestStep
{
    private readonly Operation _operation;
    private readonly ILambdaSerializer _serializer;

    internal TestStep(Operation operation, ILambdaSerializer serializer)
    {
        _operation = operation;
        _serializer = serializer;
    }

    /// <summary>The operation's unique identifier.</summary>
    public string Id => _operation.Id!;

    /// <summary>User-supplied operation name (e.g., the step name).</summary>
    public string? Name => _operation.Name;

    /// <summary>Parent operation identifier, if this operation is nested.</summary>
    public string? ParentId => _operation.ParentId;

    /// <summary>The kind of operation (Step, Wait, Callback, etc.).</summary>
    public OperationKind Kind => MapKind(_operation.Type);

    /// <summary>
    /// The sub-kind providing finer classification (e.g., "Parallel", "Map",
    /// "WaitForCallback", "WaitForCondition"). Null when not applicable.
    /// </summary>
    public string? SubKind => _operation.SubType;

    /// <summary>The terminal status of this operation.</summary>
    public string Status => _operation.Status ?? OperationStatus.Pending;

    /// <summary>
    /// The attempt number (1-based) for step operations. 0 for non-step kinds.
    /// </summary>
    public int Attempt => _operation.StepDetails?.Attempt ?? 0;

    /// <summary>When the operation started (null if not yet started).</summary>
    public DateTimeOffset? StartedAt => _operation.StartTimestamp.HasValue
        ? DateTimeOffset.FromUnixTimeMilliseconds(_operation.StartTimestamp.Value)
        : null;

    /// <summary>When the operation ended (null if not yet ended).</summary>
    public DateTimeOffset? EndedAt => _operation.EndTimestamp.HasValue
        ? DateTimeOffset.FromUnixTimeMilliseconds(_operation.EndTimestamp.Value)
        : null;

    /// <summary>Elapsed wall-clock duration, or null if timestamps are missing.</summary>
    public TimeSpan? Duration => StartedAt.HasValue && EndedAt.HasValue
        ? EndedAt - StartedAt
        : null;

    /// <summary>Child operations (linked by parent ID). Set externally by <see cref="TestResult{TOutput}"/>.</summary>
    public IReadOnlyList<TestStep> Children { get; internal set; } = Array.Empty<TestStep>();

    /// <summary>
    /// Deserializes and returns the typed result from this operation.
    /// Routes to the appropriate details property based on <see cref="Kind"/>.
    /// Returns default when no result is present.
    /// </summary>
    public T? GetResult<T>()
    {
        var serialized = Kind switch
        {
            OperationKind.Step => _operation.StepDetails?.Result,
            OperationKind.ChainedInvoke => _operation.ChainedInvokeDetails?.Result,
            OperationKind.Context => _operation.ContextDetails?.Result,
            OperationKind.Callback => _operation.CallbackDetails?.Result,
            _ => null,
        };

        if (serialized is null) return default;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
        return _serializer.Deserialize<T>(stream);
    }

    /// <summary>
    /// Returns the error from this operation, or null if no error is present.
    /// Routes to the appropriate details property based on <see cref="Kind"/>.
    /// </summary>
    public ErrorObject? GetError()
    {
        return Kind switch
        {
            OperationKind.Step => _operation.StepDetails?.Error,
            OperationKind.ChainedInvoke => _operation.ChainedInvokeDetails?.Error,
            OperationKind.Context => _operation.ContextDetails?.Error,
            OperationKind.Callback => _operation.CallbackDetails?.Error,
            _ => null,
        };
    }

    /// <summary>
    /// Returns the scheduled end time for a wait operation, or null.
    /// </summary>
    public DateTimeOffset? GetWaitEndsAt()
    {
        var ts = _operation.WaitDetails?.ScheduledEndTimestamp;
        return ts.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(ts.Value) : null;
    }

    /// <summary>
    /// Returns the callback identifier for a callback operation, or null.
    /// </summary>
    public string? GetCallbackId() => _operation.CallbackDetails?.CallbackId;

    /// <summary>
    /// Returns the function name for a chained-invoke operation, or null.
    /// </summary>
    public string? GetChainedInvokeFunctionName() => _operation.ChainedInvokeDetails is not null
        ? _operation.Name
        : null;

    private static OperationKind MapKind(string? type) => type switch
    {
        OperationTypes.Step => OperationKind.Step,
        OperationTypes.Wait => OperationKind.Wait,
        OperationTypes.Callback => OperationKind.Callback,
        OperationTypes.ChainedInvoke => OperationKind.ChainedInvoke,
        OperationTypes.Context => OperationKind.Context,
        OperationTypes.Execution => OperationKind.Execution,
        _ => OperationKind.Step,
    };
}
