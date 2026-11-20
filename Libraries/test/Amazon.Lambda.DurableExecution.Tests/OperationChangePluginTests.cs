// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary><see cref="IDurableInstrumentationPlugin.OnOperationChange"/> from checkpoint responses.</summary>
public class OperationChangePluginTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123:durable-execution:change-test";

    private sealed class ChangeRecorder : IDurableInstrumentationPlugin
    {
        public readonly List<OperationChangeInfo> Changes = new();
        public void OnOperationChange(OperationChangeInfo info) { lock (Changes) Changes.Add(info); }
    }

    private sealed class ThrowingChangePlugin : IDurableInstrumentationPlugin
    {
        public void OnOperationChange(OperationChangeInfo info) => throw new InvalidOperationException("boom");
    }

    private sealed class Factory : IDurableInstrumentationPluginFactory
    {
        private readonly IDurableInstrumentationPlugin _plugin;
        public Factory(IDurableInstrumentationPlugin plugin) => _plugin = plugin;
        public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) => _plugin;
    }

    private static DurableExecutionInvocationInput Input() => new()
    {
        DurableExecutionArn = Arn,
        InitialExecutionState = new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = "exec-0", Type = OperationTypes.Execution, Status = OperationStatuses.Started,
                    ExecutionDetails = new ExecutionDetails { InputPayload = "\"in\"" },
                },
            },
        },
    };

    /// <summary>
    /// A service that answers every checkpoint with all the callbacks started so far, the way a response that
    /// repeats earlier records would look.
    /// </summary>
    private static MockLambdaClient ServiceEchoingStartedCallbacks()
    {
        var known = new List<Amazon.Lambda.Model.Operation>();
        return new MockLambdaClient
        {
            CheckpointHandler = req =>
            {
                foreach (var u in req.Updates)
                {
                    if (u.Type == OperationTypes.Callback && u.Action == "START")
                    {
                        known.Add(new Amazon.Lambda.Model.Operation
                        {
                            Id = u.Id, Type = OperationTypes.Callback, Status = OperationStatuses.Started, Name = u.Name,
                            CallbackDetails = new Amazon.Lambda.Model.CallbackDetails { CallbackId = "cb-" + u.Name },
                        });
                    }
                }
                return new Amazon.Lambda.Model.CheckpointDurableExecutionResponse
                {
                    NewExecutionState = known.Count == 0
                        ? null
                        : new Amazon.Lambda.Model.CheckpointUpdatedExecutionState { Operations = new List<Amazon.Lambda.Model.Operation>(known) },
                };
            },
        };
    }

    private static Task<DurableExecutionInvocationOutput> RunWithTwoCallbacks(IDurableInstrumentationPlugin plugin, MockLambdaClient client)
    {
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(plugin));
        return DurableFunction.WrapAsync<string, string>(
            async (_, ctx) =>
            {
                var first = await ctx.CreateCallbackAsync<string>(name: "first");
                var second = await ctx.CreateCallbackAsync<string>(name: "second");
                return await first.GetResultAsync() + await second.GetResultAsync();
            },
            Input(), new TestLambdaContext { Serializer = new DefaultLambdaJsonSerializer() }, client, options);
    }

    [Fact]
    public async Task NewOperationFromACheckpointResponse_IsReported_WithASnapshotOfEverything()
    {
        var recorder = new ChangeRecorder();

        await RunWithTwoCallbacks(recorder, ServiceEchoingStartedCallbacks());

        var first = recorder.Changes[0];
        Assert.Equal(Arn, first.ExecutionArn);
        var updated = Assert.Single(first.UpdatedOperations).Value;
        Assert.Equal(OperationTypes.Callback, updated.Type);
        Assert.Equal("first", updated.Name);
        Assert.Equal(OperationStatuses.Started, updated.Status);
        Assert.Contains(first.Operations.Values, o => o.Type == OperationTypes.Execution);
    }

    [Fact]
    public async Task ResponseThatRepeatsAKnownStatus_IsNotReportedAgain()
    {
        var recorder = new ChangeRecorder();

        await RunWithTwoCallbacks(recorder, ServiceEchoingStartedCallbacks());

        // The second response repeats "first" unchanged and adds "second": only "second" is a change.
        Assert.Equal(2, recorder.Changes.Count);
        Assert.Equal("second", Assert.Single(recorder.Changes[1].UpdatedOperations).Value.Name);
    }

    [Fact]
    public async Task NoNewOperations_ReportsNothing()
    {
        var recorder = new ChangeRecorder();
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(recorder));

        await DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => await ctx.StepAsync((_, _) => Task.FromResult("x"), name: "s"),
            Input(), new TestLambdaContext { Serializer = new DefaultLambdaJsonSerializer() }, new MockLambdaClient(), options);

        Assert.Empty(recorder.Changes);
    }

    [Fact]
    public async Task ThrowingPlugin_DoesNotBreakTheCheckpointOrTheWorkflow()
    {
        var output = await RunWithTwoCallbacks(new ThrowingChangePlugin(), ServiceEchoingStartedCallbacks());

        Assert.Equal(InvocationStatus.Pending, output.Status);
    }
}
