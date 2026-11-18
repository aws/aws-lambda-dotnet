// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.DurableExecution.Testing;
using Xunit;
using static Amazon.Lambda.DurableExecution.OpenTelemetry.Tests.SpanCollector;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry.Tests;

/// <summary>The Execution view: operations are children of the Workflow span and export once, when they finish.</summary>
public class ExecutionViewTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123456789012:execution:test-fn:test-execution";

    private static async Task<TestResult<string>> Run(
        SpanCollector spans, Func<IDurableContext, Task<string>> workflow, Func<InvocationInfo, ExtractedContext?>? extractor = null)
    {
        var options = spans.ExecutionOptions(spans.Options(extractor ?? RemoteParent()), OtelView.Execution);
        await using var runner = new DurableTestRunner<string, string>((_, ctx) => workflow(ctx), Runner(options));
        return await runner.RunAsync("in");
    }

    private static string Id(string spanOperationId) => DeterministicIds.SpanIdFromOperationId(spanOperationId, Arn);

    [Fact]
    public async Task SingleStep_OperationsAreChildrenOfTheWorkflow_AndLinkTheInvocation()
    {
        using var spans = new SpanCollector();

        var result = await Run(spans, ctx => ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"));

        result.EnsureSucceeded();
        var workflow = spans.Single("Workflow");
        var invocation = spans.Single("Invocation");
        var charge = spans.Single("charge");
        var attempt = spans.Single("charge attempt 1");

        Assert.All(spans.Spans, s => Assert.Equal(RemoteTraceId, s.TraceId.ToHexString()));
        Assert.Equal(RemoteParentSpanId, workflow.ParentSpanId.ToHexString());
        Assert.Equal(RemoteParentSpanId, invocation.ParentSpanId.ToHexString());
        Assert.Equal(workflow.SpanId, charge.ParentSpanId);
        Assert.Equal(charge.SpanId, attempt.ParentSpanId);

        Assert.Equal(DeterministicIds.WorkflowSpanId(Arn), workflow.SpanId.ToHexString());
        Assert.Equal(Id(Tag(charge, "durable.operation.id")), charge.SpanId.ToHexString());

        // The Invocation is not a parent here. Operations and attempts link it.
        Assert.Equal(invocation.SpanId, Assert.Single(charge.Links).Context.SpanId);
        Assert.Equal(invocation.SpanId, Assert.Single(attempt.Links).Context.SpanId);
        Assert.Empty(workflow.Links);
        Assert.Empty(invocation.Links);

        Assert.Equal("SUCCEEDED", Tag(charge, "durable.operation.status"));
        Assert.Equal("1", Tag(charge, "durable.attempt.number"));
        Assert.Equal(ActivityStatusCode.Ok, charge.Status);
        Assert.Equal("SUCCEEDED", Tag(attempt, "durable.attempt.outcome"));
    }

    [Fact]
    public async Task OperationSpansEncloseTheirAttemptsAndChildren()
    {
        using var spans = new SpanCollector();

        await Run(spans, ctx => ctx.RunInChildContextAsync(
            async (child, _) => await child.StepAsync(async (_, _) => { await Task.Delay(5); return "x"; }, name: "inner"), name: "outer"));

        var outer = spans.Single("outer");
        var inner = spans.Single("inner");
        var attempt = spans.Single("inner attempt 1");
        Assert.Equal(spans.Single("Workflow").SpanId, outer.ParentSpanId);
        Assert.Equal(outer.SpanId, inner.ParentSpanId);
        Assert.Equal(inner.SpanId, attempt.ParentSpanId);
        Assert.True(outer.StartTimeUtc <= inner.StartTimeUtc);
        Assert.True(inner.StartTimeUtc <= attempt.StartTimeUtc);
        Assert.True(attempt.StartTimeUtc + attempt.Duration <= inner.StartTimeUtc + inner.Duration + TimeSpan.FromMilliseconds(1));
        Assert.True(inner.StartTimeUtc + inner.Duration <= outer.StartTimeUtc + outer.Duration + TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ExecutionSpanningInvocations_ExportsEachOperationOnce_InTheInvocationWhereItFinished()
    {
        using var spans = new SpanCollector();

        var result = await Run(spans, async ctx =>
        {
            await ctx.StepAsync((_, _) => Task.FromResult("a"), name: "first");
            await ctx.WaitAsync(TimeSpan.FromSeconds(5), name: "pause");
            await ctx.StepAsync((_, _) => Task.FromResult("b"), name: "second");
            return "done";
        });

        result.EnsureSucceeded();
        Assert.Equal(2, result.InvocationCount);
        var invocations = spans.All("Invocation").OrderBy(s => s.StartTimeUtc).ToList();

        // Replaying "first" in invocation 2 exports nothing.
        var first = Assert.Single(spans.All("first"));
        var second = Assert.Single(spans.All("second"));
        Assert.Equal(invocations[0].SpanId, Assert.Single(first.Links).Context.SpanId);
        Assert.Equal(invocations[1].SpanId, Assert.Single(second.Links).Context.SpanId);
        Assert.Single(spans.All("Workflow"));
        Assert.All(new[] { first, second }, s => Assert.Equal(spans.Single("Workflow").SpanId, s.ParentSpanId));
        Assert.Equal(spans.Spans.Count, spans.Spans.Select(s => s.SpanId).Distinct().Count());
    }

    [Fact]
    public async Task RetriedStep_HasOneOperationSpan_AndAnAttemptSpanForEachAttempt()
    {
        using var spans = new SpanCollector();
        var attempts = 0;

        var result = await Run(spans, ctx => ctx.StepAsync((_, _) =>
        {
            if (++attempts < 3) throw new InvalidOperationException("transient");
            return Task.FromResult("ok");
        }, name: "flaky", config: new StepConfig
        {
            RetryStrategy = RetryStrategy.FromDelegate((_, attempt) => RetryDecision.RetryAfter(TimeSpan.FromSeconds(1))),
        }));

        result.EnsureSucceeded();
        Assert.Equal(3, result.InvocationCount);

        // One span for the step, exported by the invocation that finished it. Its attempts were exported by the
        // invocations that ran them, parented on the step's derived id before the step span existed.
        var step = Assert.Single(spans.All("flaky"));
        var attemptSpans = Enumerable.Range(1, 3).Select(n => spans.Single($"flaky attempt {n}")).ToList();
        Assert.All(attemptSpans, a => Assert.Equal(step.SpanId, a.ParentSpanId));
        Assert.Equal(new[] { "FAILED", "FAILED", "SUCCEEDED" }, attemptSpans.Select(a => Tag(a, "durable.attempt.outcome")).ToArray());
        Assert.Equal("3", Tag(step, "durable.attempt.number"));

        // Each attempt links the invocation that ran it. The step links the one that finished it.
        var invocations = spans.All("Invocation").OrderBy(s => s.StartTimeUtc).Select(s => s.SpanId).ToList();
        Assert.Equal(invocations, attemptSpans.Select(a => a.Links.Single().Context.SpanId).ToList());
        Assert.Equal(invocations[2], step.Links.Single().Context.SpanId);
    }

    [Fact]
    public async Task FailedStep_RecordsTheErrorOnTheOperationSpan()
    {
        using var spans = new SpanCollector();

        await Run(spans, async ctx =>
        {
            try { await ctx.StepAsync<string>((_, _) => throw new InvalidOperationException("card declined"), name: "charge"); }
            catch (StepException) { }
            return "handled";
        });

        var charge = spans.Single("charge");
        Assert.Equal(ActivityStatusCode.Error, charge.Status);
        var exception = Assert.Single(charge.Events, e => e.Name == "exception");
        Assert.Equal("card declined", exception.Tags.Single(t => t.Key == "exception.message").Value);
        Assert.Equal(ActivityStatusCode.Error, spans.Single("charge attempt 1").Status);
    }

    [Fact]
    public async Task WithoutAPropagatedParent_TheWorkflowHangsOffTheSyntheticRoot()
    {
        using var spans = new SpanCollector();

        await Run(spans, ctx => ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"), _ => null);

        var root = spans.Single("DurableExecutionRoot");
        Assert.Equal(default, root.ParentSpanId);
        Assert.Equal(root.SpanId, spans.Single("Workflow").ParentSpanId);
        Assert.Equal(spans.Single("Workflow").SpanId, spans.Single("charge").ParentSpanId);
    }

    [Fact]
    public async Task UpstreamNotSampled_ExportsNothing()
    {
        using var spans = new SpanCollector();

        var result = await Run(spans, ctx => ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"),
            RemoteParent(UpstreamSampling.NotSampled));

        result.EnsureSucceeded();
        Assert.Empty(spans.Spans);
    }

    [Fact]
    public async Task UserCode_RunsUnderTheWorkflowChildContextAndAttempt_AndSpansItStartsParentOntoThem()
    {
        using var spans = new SpanCollector();
        using var source = new ActivitySource(spans.SourceName);
        string? workflowSeen = null, childSeen = null, stepSeen = null, userInChildParent = null, userInWorkflowParent = null;

        await Run(spans, async ctx =>
        {
            workflowSeen = Activity.Current?.SpanId.ToHexString();
            using (var user = source.StartActivity("user in workflow")) userInWorkflowParent = user?.ParentSpanId.ToHexString();
            await ctx.RunInChildContextAsync(async (child, _) =>
            {
                childSeen = Activity.Current?.SpanId.ToHexString();
                using (var user = source.StartActivity("user in child")) userInChildParent = user?.ParentSpanId.ToHexString();
                await child.StepAsync((_, _) => { stepSeen = Activity.Current?.OperationName; return Task.FromResult("x"); }, name: "inner");
                return 1;
            }, name: "outer");
            return "done";
        });

        Assert.Equal(DeterministicIds.WorkflowSpanId(Arn), workflowSeen);
        Assert.Equal(workflowSeen, userInWorkflowParent);
        var outer = spans.Single("outer");
        Assert.Equal(outer.SpanId.ToHexString(), childSeen);
        Assert.Equal(outer.SpanId.ToHexString(), userInChildParent);
        Assert.Equal("inner attempt 1", stepSeen);
        Assert.Null(Activity.Current);

        // The stand-ins carry identity only. They are never exported, and the user's spans are.
        Assert.DoesNotContain(spans.Spans, s => s.OperationName == "durable-context");
        Assert.Equal(workflowSeen, spans.Single("user in workflow").ParentSpanId.ToHexString());
    }

    [Fact]
    public async Task TwoOtelFactories_AreStillRejected()
    {
        using var spans = new SpanCollector();
        var options = new DurableExecutionOptions();
        options.AddOpenTelemetry(OtelView.Execution, spans.Options());
        options.AddOpenTelemetry(OtelView.Invocation, spans.Options());
        await using var runner = new DurableTestRunner<string, string>((_, _) => Task.FromResult("x"), Runner(options));

        await Assert.ThrowsAsync<PluginLoadException>(() => runner.RunAsync("in"));
    }
}

/// <summary>Calls the Execution view's hooks directly, for timing and ordering the in-memory service cannot produce.</summary>
public class ExecutionViewHookTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123456789012:execution:direct";
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 4, 30, 0, TimeSpan.Zero);

    private static OperationInfo Op(
        string id, string type, string status, string? name = null, string? parent = null, string? subType = null,
        bool replay = false, DateTimeOffset? start = null, DateTimeOffset? end = null) => new()
    {
        Id = id, Type = type, Status = status, Name = name, ParentId = parent, SubType = subType, IsReplay = replay,
        StartTimestamp = start, EndTimestamp = end,
    };

    private static OperationEndInfo End(OperationInfo op, bool replay = false) => new()
    {
        Id = op.Id, Type = op.Type, Name = op.Name, ParentId = op.ParentId, SubType = op.SubType, Status = op.Status,
        StartTimestamp = op.StartTimestamp, EndTimestamp = op.EndTimestamp, Attempt = op.Attempt, IsReplay = replay,
    };

    private static InvocationInfo Info(params OperationInfo[] updated) => new()
    {
        ExecutionArn = Arn,
        UpdatedOperations = updated.ToDictionary(o => o.Id),
        Operations = updated.ToDictionary(o => o.Id),
    };

    private static IDurableInstrumentationPlugin Start(SpanCollector spans, InvocationInfo info)
    {
        var plugin = OtelPlugin.CreateFactory(OtelView.Execution, spans.Options(RemoteParent())).CreatePlugin(info)!;
        plugin.OnInvocationStart(info);
        return plugin;
    }

    private static Task Finish(
        IDurableInstrumentationPlugin plugin, InvocationEndStatus status = InvocationEndStatus.Pending,
        params OperationInfo[] operations) =>
        plugin.OnInvocationEndAsync(new InvocationEndInfo
        {
            ExecutionArn = Arn, Status = status, Operations = operations.ToDictionary(o => o.Id),
        }, CancellationToken.None);

    [Fact]
    public async Task OperationSpan_UsesTheServicesStartAndEnd()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var step = Op("step-1", OperationTypes.Step, OperationStatuses.Succeeded, "charge", start: T0, end: T0.AddSeconds(10));

        plugin.OnOperationStart(Op("step-1", OperationTypes.Step, OperationStatuses.Started, "charge"));
        plugin.OnOperationEnd(End(step));
        await Finish(plugin);

        var charge = spans.Single("charge");
        Assert.Equal(T0, new DateTimeOffset(charge.StartTimeUtc, TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromSeconds(10), charge.Duration);
    }

    [Fact]
    public async Task EndBeforeStart_IsRaisedToTheStart()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var step = Op("step-1", OperationTypes.Step, OperationStatuses.Succeeded, "charge", start: T0, end: T0.AddSeconds(-3));

        plugin.OnOperationStart(step);
        plugin.OnOperationEnd(End(step));
        await Finish(plugin);

        // Activity reports a span with no length as one tick.
        Assert.InRange(spans.Single("charge").Duration, TimeSpan.Zero, TimeSpan.FromTicks(1));
    }

    [Fact]
    public async Task ParentStartMovesEarlier_WhenAChildStartedBeforeIt()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var parent = Op("ctx-1", OperationTypes.Context, OperationStatuses.Succeeded, "outer", start: T0.AddSeconds(5), end: T0.AddSeconds(20));
        var child = Op("step-1", OperationTypes.Step, OperationStatuses.Succeeded, "inner", parent: "ctx-1", start: T0, end: T0.AddSeconds(10));

        plugin.OnOperationStart(Op("ctx-1", OperationTypes.Context, OperationStatuses.Started, "outer", start: T0.AddSeconds(5)));
        plugin.OnOperationStart(Op("step-1", OperationTypes.Step, OperationStatuses.Started, "inner", parent: "ctx-1", start: T0));
        plugin.OnOperationEnd(End(child));
        plugin.OnOperationEnd(End(parent));
        await Finish(plugin);

        var outer = spans.Single("outer");
        Assert.Equal(T0, new DateTimeOffset(outer.StartTimeUtc, TimeSpan.Zero));
        Assert.Equal(outer.SpanId, spans.Single("inner").ParentSpanId);
    }

    [Fact]
    public async Task OperationStillOpenWhenTheInvocationSuspends_ExportsNothing_ButItsAttemptsEnd()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        plugin.OnOperationStart(Op("wait-1", OperationTypes.Wait, OperationStatuses.Started, "pause"));
        plugin.OnOperationStart(Op("step-1", OperationTypes.Step, OperationStatuses.Started, "retrying"));
        plugin.OnAttemptStart(new AttemptInfo { Id = "step-1", Type = OperationTypes.Step, Name = "retrying", Attempt = 1 });

        await Finish(plugin);

        Assert.Empty(spans.All("pause"));
        Assert.Empty(spans.All("retrying"));
        Assert.Single(spans.All("retrying attempt 1"));
        Assert.Equal("PENDING", Tag(spans.Single("Invocation"), "durable.invocation.status"));
        Assert.Empty(spans.All("Workflow"));
    }

    [Fact]
    public async Task ReplayedStep_ExportsNothing()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var step = Op("step-1", OperationTypes.Step, OperationStatuses.Succeeded, "charge", replay: true, start: T0, end: T0.AddSeconds(1));

        plugin.OnOperationStart(step);
        plugin.OnOperationEnd(End(step, replay: true));
        await Finish(plugin);

        Assert.Empty(spans.All("charge"));
    }

    [Fact]
    public async Task WaitThatElapsedWhileSuspended_IsExportedOnce_WithTheServicesTimes()
    {
        using var spans = new SpanCollector();
        var wait = Op("wait-1", OperationTypes.Wait, OperationStatuses.Succeeded, "pause", start: T0, end: T0.AddSeconds(30));
        var plugin = Start(spans, Info(wait));

        plugin.OnOperationStart(Op("wait-1", OperationTypes.Wait, OperationStatuses.Succeeded, "pause", replay: true, start: T0));
        plugin.OnOperationEnd(End(wait, replay: true));
        plugin.OnOperationEnd(End(wait, replay: false));
        await Finish(plugin);

        var pause = Assert.Single(spans.All("pause"));
        Assert.Equal(TimeSpan.FromSeconds(30), pause.Duration);
        Assert.Equal(DeterministicIds.WorkflowSpanId(Arn), pause.ParentSpanId.ToHexString());
        Assert.Equal(spans.Single("Invocation").SpanId, Assert.Single(pause.Links).Context.SpanId);
    }

    [Fact]
    public async Task CompletionTheWorkflowDidNotReach_IsExportedWhenTheInvocationEnds()
    {
        using var spans = new SpanCollector();
        var callback = Op("cb-1", OperationTypes.Callback, OperationStatuses.Failed, "approval", start: T0, end: T0.AddSeconds(2));
        var plugin = Start(spans, Info(callback));

        await Finish(plugin);

        var span = Assert.Single(spans.All("approval"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(TimeSpan.FromSeconds(2), span.Duration);
    }

    [Fact]
    public async Task CompletionWhoseParentContextNeverFinished_ButAnAncestorDid_AttachesToTheWorkflow()
    {
        using var spans = new SpanCollector();
        var callback = Op("cb-1", OperationTypes.Callback, OperationStatuses.Succeeded, "approval", parent: "ctx-1", start: T0, end: T0.AddSeconds(2));
        var plugin = Start(spans, Info(callback));
        var abandoned = Op("ctx-1", OperationTypes.Context, OperationStatuses.Started, "inner", parent: "ctx-0");
        var finishedAncestor = Op("ctx-0", OperationTypes.Context, OperationStatuses.Succeeded, "outer");

        await Finish(plugin, InvocationEndStatus.Succeeded, abandoned, finishedAncestor);

        var approval = spans.Single("approval");
        Assert.Equal(spans.Single("Workflow").SpanId, approval.ParentSpanId);
    }

    [Fact]
    public async Task InvocationSpan_EndsNoEarlierThanTheLatestBoundaryItObserved()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var future = DateTimeOffset.UtcNow.AddMinutes(10);
        var step = Op("step-1", OperationTypes.Step, OperationStatuses.Succeeded, "charge", start: future.AddSeconds(-1), end: future);

        plugin.OnOperationStart(step);
        plugin.OnOperationEnd(End(step));
        await Finish(plugin);

        var invocation = spans.Single("Invocation");
        Assert.True(invocation.StartTimeUtc + invocation.Duration >= future.UtcDateTime - TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task PropagationHeader_NamesTheOperationsDerivedSpan()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());

        var header = plugin.ProvidePropagationMetadata(new PropagationInput
        {
            ExecutionArn = Arn, OperationId = "invoke-1", TargetFunctionName = "other",
        });

        Assert.Equal(
            $"Root=1-5759e988-bd862e3fe1be46a994272793;Parent={DeterministicIds.SpanIdFromOperationId("invoke-1", Arn)};Sampled=1",
            header);
        await Finish(plugin);
    }
}
