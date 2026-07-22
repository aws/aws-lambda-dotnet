// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;

namespace Amazon.Lambda.TestTool.Services.DurableExecution;

/// <summary>
/// In-memory store for operations recorded during a durable execution. Each execution
/// (keyed by ARN) maintains its own isolated operation set and checkpoint token.
/// </summary>
/// <remarks>
/// Ported from <c>Amazon.Lambda.DurableExecution.Testing.InMemoryOperationStore</c> (which is
/// <c>internal</c>). The logic is intentionally identical; it operates on the public
/// <see cref="Operation"/> wire type so the emulator can serialize state responses directly.
/// </remarks>
internal sealed class InMemoryOperationStore
{
    private readonly Dictionary<string, ExecutionData> _executions = new();

    // A single lock guards both the per-ARN dictionary and each ExecutionData's collections.
    // Writes are normally serialized by the runtime's single-reader checkpoint batcher, but
    // parallel/map workflows and the snapshot reads below can interleave, so we lock to keep
    // the Dictionary/List internally consistent.
    private readonly object _gate = new();

    public string CurrentToken(string arn)
    {
        lock (_gate)
            return GetOrCreate(arn).CheckpointToken;
    }

    /// <summary>
    /// Returns a snapshot copy of the operations for the execution. The copy is detached from
    /// the backing list so callers can iterate safely while the store continues to mutate.
    /// </summary>
    public IReadOnlyList<Operation> GetAllOperations(string arn)
    {
        lock (_gate)
            return GetOrCreate(arn).Operations.ToList();
    }

    public Operation? GetOperation(string arn, string operationId)
    {
        lock (_gate)
        {
            var data = GetOrCreate(arn);
            return data.OperationMap.TryGetValue(operationId, out var op) ? op : null;
        }
    }

    public void Upsert(string arn, Operation operation)
    {
        lock (_gate)
        {
            var data = GetOrCreate(arn);
            if (data.OperationMap.TryGetValue(operation.Id!, out var existing))
            {
                var index = data.Operations.IndexOf(existing);
                data.Operations[index] = operation;
                data.OperationMap[operation.Id!] = operation;
            }
            else
            {
                data.Operations.Add(operation);
                data.OperationMap[operation.Id!] = operation;
            }
        }
    }

    public string IncrementToken(string arn)
    {
        lock (_gate)
        {
            var data = GetOrCreate(arn);
            data.TokenCounter++;
            data.CheckpointToken = data.TokenCounter.ToString();
            return data.CheckpointToken;
        }
    }

    public int OperationCount(string arn)
    {
        lock (_gate)
            return GetOrCreate(arn).Operations.Count;
    }

    /// <summary>True once an execution with this ARN has been created.</summary>
    public bool Exists(string arn)
    {
        lock (_gate)
            return _executions.ContainsKey(arn);
    }

    private ExecutionData GetOrCreate(string arn)
    {
        if (!_executions.TryGetValue(arn, out var data))
        {
            data = new ExecutionData();
            _executions[arn] = data;
        }
        return data;
    }

    private sealed class ExecutionData
    {
        public readonly List<Operation> Operations = new();
        public readonly Dictionary<string, Operation> OperationMap = new();
        public string CheckpointToken = "0";
        public int TokenCounter;
    }
}
