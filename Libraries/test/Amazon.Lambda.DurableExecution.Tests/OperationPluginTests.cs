// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>Operation, attempt and wrap hooks reported while a workflow runs.</summary>
public class OperationPluginTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123:durable-execution:operation-plugin-test";

    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private sealed class Recorder : IDurableInstrumentationPlugin
    {
        public readonly List<string> Events = new();
        public readonly List<OperationInfo> OperationStarts = new();
        public readonly List<OperationEndInfo> OperationEnds = new();
        public readonly List<AttemptInfo> AttemptStarts = new();
        public readonly List<AttemptEndInfo> AttemptEnds = new();
        public readonly List<UserCodeInfo> Wraps = new();
        private readonly object _lock = new();

        private void Add(string e) { lock (_lock) Events.Add(e); }

        public void OnOperationStart(OperationInfo info) { lock (_lock) OperationStarts.Add(info); Add($"start:{info.Name ?? info.Type}"); }
        public void OnOperationEnd(OperationEndInfo info) { lock (_lock) OperationEnds.Add(info); Add($"end:{info.Name ?? info.Type}:{info.Status}"); }
        public void OnAttemptStart(AttemptInfo info) { lock (_lock) AttemptStarts.Add(info); Add($"attemptStart:{info.Name}:{info.Attempt}"); }
        public void OnAttemptEnd(AttemptEndInfo info) { lock (_lock) AttemptEnds.Add(info); Add($"attemptEnd:{info.Name}:{info.Attempt}:{info.Outcome}"); }

        public async Task WrapAsync(UserCodeInfo info, Func<Task> next)
        {
            lock (_lock) Wraps.Add(info);
            Add($"wrap>{info.Kind}:{info.Operation?.Name}");
            await next();
            Add($"wrap<{info.Kind}:{info.Operation?.Name}");
        }
    }

    private sealed class Factory : IDurableInstrumentationPluginFactory
    {
        private readonly IDurableInstrumentationPlugin _plugin;
        public Factory(IDurableInstrumentationPlugin plugin) => _plugin = plugin;
        public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) => _plugin;
    }

    private static TestLambdaContext Context() => new() { Serializer = new DefaultLambdaJsonSerializer() };

    private static DurableExecutionInvocationInput Input(params Operation[] history)
    {
        var ops = new List<Operation>
        {
            new()
            {
                Id = "exec-0",
                Type = OperationTypes.Execution,
                Status = OperationStatuses.Started,
                ExecutionDetails = new ExecutionDetails { InputPayload = "\"in\"" },
            },
        };
        ops.AddRange(history);
        return new DurableExecutionInvocationInput
        {
            DurableExecutionArn = Arn,
            InitialExecutionState = new InitialExecutionState { Operations = ops },
        };
    }

    private static Task<DurableExecutionInvocationOutput> Run(
        Recorder recorder, Func<IDurableContext, Task<string>> workflow, params Operation[] history)
    {
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(recorder));
        return DurableFunction.WrapAsync<string, string>(
            (_, ctx) => workflow(ctx), Input(history), Context(), new MockLambdaClient(), options);
    }

    [Fact]
    public async Task Step_ReportsOperationAndAttemptInOrder_WithIdentity()
    {
        var recorder = new Recorder();

        var output = await Run(recorder, ctx => ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"));

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.Equal(
            new[]
            {
                "wrap>Invocation:",
                "start:charge", "attemptStart:charge:1", "wrap>Attempt:charge", "wrap<Attempt:charge",
                "attemptEnd:charge:1:Succeeded", $"end:charge:{OperationStatuses.Succeeded}",
                "wrap<Invocation:",
            },
            recorder.Events);

        var start = Assert.Single(recorder.OperationStarts);
        Assert.Equal(IdAt(1), start.Id);
        Assert.Equal(OperationTypes.Step, start.Type);
        Assert.Equal(OperationSubTypes.Step, start.SubType);
        Assert.Null(start.ParentId);
        Assert.False(start.IsReplay);

        var end = Assert.Single(recorder.OperationEnds);
        Assert.Null(end.Error);
        Assert.Equal(1, end.Attempt);
        Assert.Equal(OperationUserCode(recorder, UserCodeKind.Attempt), IdAt(1));
    }

    private static string? OperationUserCode(Recorder recorder, UserCodeKind kind) =>
        recorder.Wraps.Single(w => w.Kind == kind).Operation?.Id;

    [Fact]
    public async Task Step_TerminalFailure_ReportsFailedAttemptAndOperation_AndThrowsToTheWorkflow()
    {
        var recorder = new Recorder();
        StepException? caught = null;

        await Run(recorder, async ctx =>
        {
            try { await ctx.StepAsync<string>((_, _) => throw new InvalidOperationException("nope"), name: "charge"); }
            catch (StepException ex) { caught = ex; }
            return "handled";
        });

        Assert.NotNull(caught);
        var attemptEnd = Assert.Single(recorder.AttemptEnds);
        Assert.Equal(AttemptOutcome.Failed, attemptEnd.Outcome);
        Assert.IsType<InvalidOperationException>(attemptEnd.Exception);

        var end = Assert.Single(recorder.OperationEnds);
        Assert.Equal(OperationStatuses.Failed, end.Status);
        Assert.Equal("nope", end.Error!.ErrorMessage);
        Assert.IsType<StepException>(end.Exception);
    }

    [Fact]
    public async Task Step_RetryScheduled_EndsTheAttemptButNotTheOperation()
    {
        var recorder = new Recorder();
        var retryOnce = new StepConfig
        {
            RetryStrategy = RetryStrategy.FromDelegate((_, attempt) => attempt < 2
                ? RetryDecision.RetryAfter(TimeSpan.FromSeconds(30))
                : RetryDecision.DoNotRetry()),
        };

        var output = await Run(recorder, ctx => ctx.StepAsync<string>(
            (_, _) => throw new InvalidOperationException("transient"), name: "flaky", config: retryOnce));

        Assert.Equal(InvocationStatus.Pending, output.Status);
        Assert.Single(recorder.AttemptEnds);
        Assert.Equal(AttemptOutcome.Failed, recorder.AttemptEnds[0].Outcome);
        Assert.Empty(recorder.OperationEnds);
    }

    [Fact]
    public async Task Step_ResumedAfterRetry_ReportsTheSecondAttempt()
    {
        var recorder = new Recorder();
        var ready = new Operation
        {
            Id = IdAt(1), Type = OperationTypes.Step, Name = "flaky", Status = OperationStatuses.Ready,
            StepDetails = new StepDetails { Attempt = 1 },
        };

        await Run(recorder, ctx => ctx.StepAsync((_, _) => Task.FromResult("ok"), name: "flaky"), ready);

        Assert.Equal(2, Assert.Single(recorder.AttemptStarts).Attempt);
        Assert.Equal(OperationStatuses.Succeeded, Assert.Single(recorder.OperationEnds).Status);
        Assert.False(recorder.OperationStarts[0].IsReplay);
    }

    [Fact]
    public async Task Step_Replayed_ReportsReplayStartAndEnd_WithoutRunningAnAttempt()
    {
        var recorder = new Recorder();
        var ran = false;
        var done = new Operation
        {
            Id = IdAt(1), Type = OperationTypes.Step, Name = "charge", Status = OperationStatuses.Succeeded,
            StartTimestamp = 1_700_000_000_000, EndTimestamp = 1_700_000_005_000,
            StepDetails = new StepDetails { Result = "\"v\"" },
        };

        await Run(recorder, ctx => ctx.StepAsync((_, _) => { ran = true; return Task.FromResult("v"); }, name: "charge"), done);

        Assert.False(ran);
        Assert.Empty(recorder.AttemptStarts);
        Assert.True(recorder.OperationStarts.Single().IsReplay);
        var end = Assert.Single(recorder.OperationEnds);
        Assert.True(end.IsReplay);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000), end.StartTimestamp);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_005_000), end.EndTimestamp);
    }

    [Fact]
    public async Task Wait_Suspends_ReportsStartButNoEnd()
    {
        var recorder = new Recorder();

        var output = await Run(recorder, async ctx => { await ctx.WaitAsync(TimeSpan.FromMinutes(5), name: "pause"); return "x"; });

        Assert.Equal(InvocationStatus.Pending, output.Status);
        var start = Assert.Single(recorder.OperationStarts);
        Assert.Equal(OperationTypes.Wait, start.Type);
        Assert.Empty(recorder.OperationEnds);
    }

    [Fact]
    public async Task ChildContext_NestsStartsAndEnds_AndWrapsTheChildFunction()
    {
        var recorder = new Recorder();

        await Run(recorder, ctx => ctx.RunInChildContextAsync(
            async (child, _) => await child.StepAsync((_, _) => Task.FromResult("inner"), name: "inner"),
            name: "outer"));

        // parent start -> child start -> child end -> parent end
        var order = recorder.Events.Where(e => e.StartsWith("start:") || e.StartsWith("end:")).ToArray();
        Assert.Equal(new[] { "start:outer", "start:inner", $"end:inner:{OperationStatuses.Succeeded}", $"end:outer:{OperationStatuses.Succeeded}" }, order);

        var inner = recorder.OperationStarts.Single(o => o.Name == "inner");
        var outer = recorder.OperationStarts.Single(o => o.Name == "outer");
        Assert.Equal(outer.Id, inner.ParentId);
        Assert.Equal(OperationTypes.Context, outer.Type);

        var childWrap = recorder.Wraps.Single(w => w.Kind == UserCodeKind.ChildContext);
        Assert.Equal(outer.Id, childWrap.Operation!.Id);
    }

    [Fact]
    public async Task WrapAsync_AmbientContextIsVisibleToUserCode_AndNotAfterIt()
    {
        var seen = new List<string?>();
        var ambient = new AsyncLocal<string?>();
        var plugin = new AmbientPlugin(ambient);
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(plugin));

        string? afterStep = null;
        await DurableFunction.WrapAsync<string, string>(async (_, ctx) =>
        {
            seen.Add(ambient.Value); // inside the invocation wrap
            await ctx.StepAsync((_, _) => { seen.Add(ambient.Value); return Task.FromResult("s"); }, name: "step");
            afterStep = ambient.Value;
            return "done";
        }, Input(), Context(), new MockLambdaClient(), options);

        Assert.Equal(new[] { "Invocation", "Attempt" }, seen);
        Assert.Equal("Invocation", afterStep);
    }

    private sealed class AmbientPlugin : IDurableInstrumentationPlugin
    {
        private readonly AsyncLocal<string?> _ambient;
        public AmbientPlugin(AsyncLocal<string?> ambient) => _ambient = ambient;

        public async Task WrapAsync(UserCodeInfo info, Func<Task> next)
        {
            var previous = _ambient.Value;
            _ambient.Value = info.Kind.ToString();
            try { await next(); }
            finally { _ambient.Value = previous; }
        }
    }

    [Fact]
    public async Task Parallel_ReportsTheOperationAndEveryBranch()
    {
        var recorder = new Recorder();

        await Run(recorder, async ctx =>
        {
            await ctx.ParallelAsync<string>(new Func<IDurableContext, CancellationToken, Task<string>>[]
            {
                async (c, _) => await c.StepAsync((_, _) => Task.FromResult("a"), name: "a"),
                async (c, _) => await c.StepAsync((_, _) => Task.FromResult("b"), name: "b"),
            }, name: "fan");
            return "done";
        });

        var fan = recorder.OperationStarts.Single(o => o.Name == "fan");
        Assert.Equal(OperationSubTypes.Parallel, fan.SubType);
        Assert.Equal(2, recorder.OperationStarts.Count(o => o.SubType == OperationSubTypes.ParallelBranch && o.ParentId == fan.Id));
        Assert.Equal(OperationStatuses.Succeeded, recorder.OperationEnds.Single(o => o.Id == fan.Id).Status);
        Assert.Equal(recorder.OperationStarts.Count, recorder.OperationEnds.Count);
    }

    [Fact]
    public async Task IncrementalParallel_ReportsTheOperationAndItsBranches()
    {
        var recorder = new Recorder();

        await Run(recorder, async ctx =>
        {
            await using (var parallel = ctx.CreateParallel(name: "fan"))
            {
                _ = parallel.Branch("a", async (_, _) => { await Task.Yield(); return 1; });
                _ = parallel.Branch("b", async (_, _) => { await Task.Yield(); return 2; });
                await parallel.CompleteAsync();
            }
            return "done";
        });

        var fan = recorder.OperationStarts.Single(o => o.Name == "fan");
        Assert.Equal(OperationSubTypes.Parallel, fan.SubType);
        Assert.Equal(2, recorder.OperationStarts.Count(o => o.ParentId == fan.Id));
        Assert.Equal(OperationStatuses.Succeeded, recorder.OperationEnds.Single(o => o.Id == fan.Id).Status);
        // The operation starts first and ends last.
        Assert.Equal($"start:fan", recorder.Events.First(e => e.StartsWith("start:")));
        Assert.Equal($"end:fan:{OperationStatuses.Succeeded}", recorder.Events.Last(e => e.StartsWith("end:")));
    }

    [Fact]
    public async Task Step_CancelledByTheCaller_IsNotReportedAsAFailedAttemptOrOperation()
    {
        var recorder = new Recorder();
        using var cts = new CancellationTokenSource();

        await Run(recorder, async ctx =>
        {
            await ctx.StepAsync<string>((_, _) =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }, name: "cancelled", cancellationToken: cts.Token);
            return "x";
        });

        Assert.Single(recorder.AttemptStarts);
        Assert.Empty(recorder.AttemptEnds);
        Assert.Empty(recorder.OperationEnds);
    }

    [Fact]
    public async Task NoPlugins_RunsWorkflowWithoutAnyHooks()
    {
        var output = await DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => await ctx.StepAsync((_, _) => Task.FromResult("v"), name: "s"),
            Input(), Context(), new MockLambdaClient());

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
    }
}
