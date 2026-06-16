// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// In-memory store for operations recorded during a test execution.
/// Each execution (keyed by ARN) maintains its own isolated operation set.
/// </summary>
internal sealed class InMemoryOperationStore
{
    private readonly Dictionary<string, ExecutionData> _executions = new();

    public string CurrentToken(string arn)
    {
        return GetOrCreate(arn).CheckpointToken;
    }

    public IReadOnlyList<Operation> GetAllOperations(string arn)
    {
        return GetOrCreate(arn).Operations;
    }

    public Operation? GetOperation(string arn, string operationId)
    {
        var data = GetOrCreate(arn);
        return data.OperationMap.TryGetValue(operationId, out var op) ? op : null;
    }

    public void Upsert(string arn, Operation operation)
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

    public string IncrementToken(string arn)
    {
        var data = GetOrCreate(arn);
        data.TokenCounter++;
        data.CheckpointToken = data.TokenCounter.ToString();
        return data.CheckpointToken;
    }

    public int OperationCount(string arn)
    {
        return GetOrCreate(arn).Operations.Count;
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
