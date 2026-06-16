// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Services;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Drives a durable workflow handler to a terminal state by repeatedly invoking
/// the internal DurableFunction.WrapAsync overload with the in-memory service client.
/// </summary>
internal sealed class ExecutionOrchestrator<TInput, TOutput>
{
    private readonly Func<TInput, IDurableContext, Task<TOutput>> _handler;
    private readonly InMemoryOperationStore _store;
    private readonly IDurableServiceClient _serviceClient;
    private readonly ILambdaContext _lambdaContext;
    private readonly TestRunnerOptions _options;
    private readonly ILambdaSerializer _serializer;

    public ExecutionOrchestrator(
        Func<TInput, IDurableContext, Task<TOutput>> handler,
        InMemoryOperationStore store,
        IDurableServiceClient serviceClient,
        ILambdaContext lambdaContext,
        TestRunnerOptions options,
        ILambdaSerializer serializer)
    {
        _handler = handler;
        _store = store;
        _serviceClient = serviceClient;
        _lambdaContext = lambdaContext;
        _options = options;
        _serializer = serializer;
    }

    public async Task<TestResult<TOutput>> DriveToTerminalAsync(
        string arn,
        TInput input,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        SeedExecutionOperation(arn, input);

        var invocationCount = 0;
        DurableExecutionInvocationOutput output;

        while (true)
        {
            timeoutCts.Token.ThrowIfCancellationRequested();

            if (invocationCount >= _options.MaxInvocations)
            {
                throw new TestExecutionLimitException(
                    _options.MaxInvocations, _store.OperationCount(arn));
            }

            var invocationInput = BuildInvocationInput(arn);

            output = await DurableFunction.WrapAsync<TInput, TOutput>(
                _handler, invocationInput, _lambdaContext, _serviceClient);

            invocationCount++;

            if (output.Status != InvocationStatus.Pending)
                break;
        }

        return BuildResult(arn, output, invocationCount);
    }

    private void SeedExecutionOperation(string arn, TInput input)
    {
        var serializedInput = SerializeToString(input);
        _store.Upsert(arn, new Operation
        {
            Id = "exec-0",
            Type = OperationTypes.Execution,
            Status = OperationStatuses.Started,
            ExecutionDetails = new ExecutionDetails { InputPayload = serializedInput }
        });
    }

    private DurableExecutionInvocationInput BuildInvocationInput(string arn)
    {
        return new DurableExecutionInvocationInput
        {
            DurableExecutionArn = arn,
            CheckpointToken = _store.CurrentToken(arn),
            InitialExecutionState = new InitialExecutionState
            {
                Operations = _store.GetAllOperations(arn).ToList(),
                NextMarker = null,
            }
        };
    }

    private string SerializeToString(TInput value)
    {
        using var stream = new MemoryStream();
        _serializer.Serialize(value, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private TestResult<TOutput> BuildResult(string arn, DurableExecutionInvocationOutput output, int invocationCount)
    {
        TOutput? result = default;
        if (output.Status == InvocationStatus.Succeeded && output.Result is not null)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(output.Result));
            result = _serializer.Deserialize<TOutput>(stream);
        }

        var allOps = _store.GetAllOperations(arn);
        var steps = allOps
            .Where(o => o.Type != OperationTypes.Execution)
            .Select(o => new TestStep(o, _serializer))
            .ToList();

        return new TestResult<TOutput>(
            status: output.Status,
            result: result,
            error: output.Error,
            durableExecutionArn: arn,
            invocationCount: invocationCount,
            steps: steps);
    }
}
