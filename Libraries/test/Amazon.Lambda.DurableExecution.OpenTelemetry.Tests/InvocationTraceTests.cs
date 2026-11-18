// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.DurableExecution.Testing;
using Xunit;
using static Amazon.Lambda.DurableExecution.OpenTelemetry.Tests.SpanCollector;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry.Tests;

/// <summary>The spans of the Invocation view, produced by running workflows on the in-memory test service.</summary>
public class InvocationTraceTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123456789012:execution:test-fn:test-execution";

    private static async Task<TestResult<string>> Run(
        SpanCollector spans, Func<IDurableContext, Task<string>> workflow, Func<InvocationInfo, ExtractedContext?>? extractor = null)
    {
        var options = spans.ExecutionOptions(spans.Options(extractor));
        await using var runner = new DurableTestRunner<string, string>((_, ctx) => workflow(ctx), Runner(options));
        return await runner.RunAsync("in");
    }

    [Fact]
    public async Task SingleStep_EmitsWorkflowInvocationOperationAndAttempt_OnTheUpstreamTrace()
    {
        using var spans = new SpanCollector();

        var result = await Run(spans, ctx => ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"), RemoteParent());

        result.EnsureSucceeded();
        var workflow = spans.Single("Workflow");
        var invocation = spans.Single("Invocation");
        var charge = spans.Single("charge");
        var attempt = spans.Single("charge attempt 1");

        Assert.All(spans.Spans, s => Assert.Equal(RemoteTraceId, s.TraceId.ToHexString()));
        Assert.All(spans.Spans, s => Assert.Equal(ActivityKind.Internal, s.Kind));

        // Workflow and Invocation join the upstream span. Operations nest under the Invocation.
        Assert.Equal(RemoteParentSpanId, workflow.ParentSpanId.ToHexString());
        Assert.Equal(RemoteParentSpanId, invocation.ParentSpanId.ToHexString());
        Assert.Equal(invocation.SpanId, charge.ParentSpanId);
        Assert.Equal(charge.SpanId, attempt.ParentSpanId);
        Assert.Empty(spans.All("DurableExecutionRoot"));

        // Ids that other invocations must be able to name.
        Assert.Equal(DeterministicIds.WorkflowSpanId(Arn), workflow.SpanId.ToHexString());
        Assert.Equal(DeterministicIds.SpanIdFromOperationId(Tag(charge, "durable.operation.id"), Arn), charge.SpanId.ToHexString());

        // Operations and attempts link the Workflow span.
        Assert.Equal(workflow.SpanId, Assert.Single(charge.Links).Context.SpanId);
        Assert.Equal(workflow.SpanId, Assert.Single(attempt.Links).Context.SpanId);
        Assert.Empty(invocation.Links);
        Assert.Empty(workflow.Links);
    }

    [Fact]
    public async Task Attributes_StatusAndTiming_AreSet()
    {
        using var spans = new SpanCollector();

        await Run(spans, ctx => ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"), RemoteParent());

        var workflow = spans.Single("Workflow");
        Assert.Equal("SUCCEEDED", Tag(workflow, "durable.execution.status"));
        Assert.Equal(Arn, Tag(workflow, "durable.execution.arn"));
        Assert.Equal(ActivityStatusCode.Ok, workflow.Status);

        var invocation = spans.Single("Invocation");
        Assert.Equal("True", Tag(invocation, "durable.invocation.first"));
        Assert.Equal("SUCCEEDED", Tag(invocation, "durable.invocation.status"));
        Assert.Equal(ActivityStatusCode.Ok, invocation.Status);

        var charge = spans.Single("charge");
        Assert.Equal("STEP", Tag(charge, "durable.operation.type"));
        Assert.Equal("Step", Tag(charge, "durable.operation.subtype"));
        Assert.Equal("charge", Tag(charge, "durable.operation.name"));
        Assert.Equal("SUCCEEDED", Tag(charge, "durable.operation.status"));
        Assert.Equal("1", Tag(charge, "durable.attempt.number"));
        Assert.Equal(ActivityStatusCode.Ok, charge.Status);

        var attempt = spans.Single("charge attempt 1");
        Assert.Equal("SUCCEEDED", Tag(attempt, "durable.attempt.outcome"));
        Assert.False(HasTag(attempt, "durable.operation.status"));

        // The operation sits inside the invocation, and the attempt inside the operation.
        Assert.True(charge.StartTimeUtc >= invocation.StartTimeUtc);
        Assert.True(charge.StartTimeUtc + charge.Duration <= invocation.StartTimeUtc + invocation.Duration);
        Assert.True(attempt.StartTimeUtc >= charge.StartTimeUtc);
        Assert.True(attempt.StartTimeUtc + attempt.Duration <= charge.StartTimeUtc + charge.Duration);
    }

    [Fact]
    public async Task WithoutAPropagatedParent_AnArnDerivedRootAnchorsTheTrace()
    {
        using var spans = new SpanCollector();

        await Run(spans, ctx => ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"));

        var root = spans.Single("DurableExecutionRoot");
        Assert.Equal(default, root.ParentSpanId);
        Assert.Equal(DeterministicIds.ExecutionRootSpanId(Arn), root.SpanId.ToHexString());
        Assert.Equal("True", Tag(root, "durable.execution.synthetic_root"));

        Assert.Equal(root.SpanId, spans.Single("Workflow").ParentSpanId);
        Assert.Equal(root.SpanId, spans.Single("Invocation").ParentSpanId);
        Assert.All(spans.Spans, s => Assert.Equal(root.TraceId, s.TraceId));
        Assert.True(DeterministicIds.IsValidTraceId(root.TraceId.ToHexString()));
    }

    [Fact]
    public async Task FailedStep_RecordsTheErrorOnTheOperationAndAttempt()
    {
        using var spans = new SpanCollector();

        var result = await Run(spans, async ctx =>
        {
            try { await ctx.StepAsync<string>((_, _) => throw new InvalidOperationException("card declined"), name: "charge"); }
            catch (StepException) { }
            return "handled";
        }, RemoteParent());

        result.EnsureSucceeded();
        var charge = spans.Single("charge");
        Assert.Equal(ActivityStatusCode.Error, charge.Status);
        Assert.Equal("FAILED", Tag(charge, "durable.operation.status"));
        var exception = Assert.Single(charge.Events, e => e.Name == "exception");
        Assert.Equal("card declined", exception.Tags.Single(t => t.Key == "exception.message").Value);
        Assert.Equal("System.InvalidOperationException", exception.Tags.Single(t => t.Key == "exception.type").Value);

        var attempt = spans.Single("charge attempt 1");
        Assert.Equal(ActivityStatusCode.Error, attempt.Status);
        Assert.Equal("FAILED", Tag(attempt, "durable.attempt.outcome"));
        Assert.Contains(attempt.Events, e => e.Name == "exception");

        // The workflow handled the failure, so the execution and invocation still succeeded.
        Assert.Equal(ActivityStatusCode.Ok, spans.Single("Workflow").Status);
    }

    [Fact]
    public async Task FailedWorkflow_MarksInvocationAndWorkflowAsErrors()
    {
        using var spans = new SpanCollector();

        var result = await Run(spans, _ => throw new InvalidOperationException("workflow broke"), RemoteParent());

        Assert.NotEqual(InvocationStatus.Succeeded, result.Status);
        var invocation = spans.Single("Invocation");
        Assert.Equal("FAILED", Tag(invocation, "durable.invocation.status"));
        Assert.Equal(ActivityStatusCode.Error, invocation.Status);
        var workflow = spans.Single("Workflow");
        Assert.Equal("FAILED", Tag(workflow, "durable.execution.status"));
        Assert.Equal(ActivityStatusCode.Error, workflow.Status);
        // Failures of the workflow itself set the status without an exception event.
        Assert.DoesNotContain(workflow.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task ChildContextAndParallel_NestUnderTheirParents()
    {
        using var spans = new SpanCollector();

        await Run(spans, async ctx =>
        {
            await ctx.RunInChildContextAsync(async (child, _) => await child.StepAsync((_, _) => Task.FromResult("x"), name: "inner"), name: "outer");
            await ctx.ParallelAsync<string>(new Func<IDurableContext, CancellationToken, Task<string>>[]
            {
                async (c, _) => await c.StepAsync((_, _) => Task.FromResult("a"), name: "branch-a"),
            }, name: "fan");
            return "done";
        }, RemoteParent());

        var outer = spans.Single("outer");
        Assert.Equal(spans.Single("Invocation").SpanId, outer.ParentSpanId);
        Assert.Equal(outer.SpanId, spans.Single("inner").ParentSpanId);
        Assert.Equal("CONTEXT", Tag(outer, "durable.operation.type"));

        var fan = spans.Single("fan");
        Assert.Equal("Parallel", Tag(fan, "durable.operation.subtype"));
        var branch = spans.Spans.Single(x => Tag(x, "durable.operation.subtype") == "ParallelBranch");
        Assert.Equal(fan.SpanId, branch.ParentSpanId);
        Assert.Equal(branch.SpanId, spans.Single("branch-a").ParentSpanId);
    }

    [Fact]
    public async Task ExecutionSpanningInvocations_ShareOneTrace_AndReplayedStepsLinkTheirFirstSegment()
    {
        using var spans = new SpanCollector();

        var result = await Run(spans, async ctx =>
        {
            await ctx.StepAsync((_, _) => Task.FromResult("a"), name: "first");
            await ctx.WaitAsync(TimeSpan.FromSeconds(5), name: "pause");
            await ctx.StepAsync((_, _) => Task.FromResult("b"), name: "second");
            return "done";
        }, RemoteParent());

        result.EnsureSucceeded();
        Assert.Equal(2, result.InvocationCount);
        Assert.All(spans.Spans, s => Assert.Equal(RemoteTraceId, s.TraceId.ToHexString()));

        var invocations = spans.All("Invocation").OrderBy(s => s.StartTimeUtc).ToList();
        Assert.Equal(2, invocations.Count);
        Assert.Equal("PENDING", Tag(invocations[0], "durable.invocation.status"));
        Assert.Equal("SUCCEEDED", Tag(invocations[1], "durable.invocation.status"));
        Assert.Equal("True", Tag(invocations[0], "durable.invocation.first"));
        Assert.Equal("False", Tag(invocations[1], "durable.invocation.first"));

        // Only the invocation that finishes the execution exports the Workflow span.
        Assert.Single(spans.All("Workflow"));

        // "first" ran in invocation 1 under its derived id. Invocation 2 reaches it again by replay: a new span
        // that links that id and the Workflow span.
        var segments = spans.All("first").OrderBy(s => s.StartTimeUtc).ToList();
        Assert.Equal(2, segments.Count);
        Assert.NotEqual(segments[0].SpanId, segments[1].SpanId);
        Assert.Equal(invocations[1].SpanId, segments[1].ParentSpanId);
        Assert.Equal(
            new[] { segments[0].SpanId, spans.Single("Workflow").SpanId },
            segments[1].Links.Select(l => l.Context.SpanId).ToArray());

        // The wait started in invocation 1. Invocation 2 reaches it again, but a wait that is only replayed is not
        // exported a second time.
        var pause = Assert.Single(spans.All("pause"));
        Assert.Equal(invocations[0].SpanId, pause.ParentSpanId);
    }

    [Fact]
    public async Task RetriedStep_NeverReusesASpanIdAcrossInvocations()
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
        }), RemoteParent());

        result.EnsureSucceeded();
        Assert.Equal(3, result.InvocationCount);
        // Every span in the trace has its own id, including the segments of the retried step.
        Assert.Equal(spans.Spans.Count, spans.Spans.Select(s => s.SpanId).Distinct().Count());

        var segments = spans.All("flaky").OrderBy(s => s.StartTimeUtc).ToList();
        Assert.Equal(3, segments.Count);
        var firstSegmentId = segments[0].SpanId;
        Assert.All(segments.Skip(1), s => Assert.Equal(firstSegmentId, s.Links.First().Context.SpanId));
        // The attempt number is set when the operation finishes, so only the last segment has one.
        Assert.Equal("3", Tag(segments[2], "durable.attempt.number"));
        Assert.Equal("SUCCEEDED", Tag(segments[2], "durable.operation.status"));
    }

    [Fact]
    public async Task UpstreamNotSampled_ExportsNothingAndStillRuns()
    {
        using var spans = new SpanCollector();

        var result = await Run(spans, ctx => ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"),
            RemoteParent(UpstreamSampling.NotSampled));

        result.EnsureSucceeded();
        Assert.Empty(spans.Spans);
    }

    [Fact]
    public async Task NoListener_RunsTheWorkflowWithoutSpans()
    {
        var options = new DurableExecutionOptions();
        options.AddOpenTelemetry(OtelView.Invocation, new OtelPluginOptions { InstrumentationName = "nobody-listens-" + Guid.NewGuid().ToString("N") });
        await using var runner = new DurableTestRunner<string, string>(
            async (_, ctx) => await ctx.StepAsync((_, _) => Task.FromResult("v"), name: "charge"), Runner(options));

        var result = await runner.RunAsync("in");

        result.EnsureSucceeded();
    }

    [Fact]
    public async Task FlushIsCalledAfterTheInvocationSpanEnded_OncePerInvocation()
    {
        var invocationEndedWhenFlushed = new List<bool>();
        var timeouts = new List<TimeSpan>();

        using var flushing = new SpanCollector();
        var options = flushing.ExecutionOptions(flushing.Options(RemoteParent(), flush: (timeout, _) =>
        {
            timeouts.Add(timeout);
            invocationEndedWhenFlushed.Add(flushing.All("Invocation").Count == invocationEndedWhenFlushed.Count + 1);
            return Task.CompletedTask;
        }));
        await using var runner = new DurableTestRunner<string, string>(
            async (_, ctx) => { await ctx.WaitAsync(TimeSpan.FromSeconds(5), name: "pause"); return "done"; }, Runner(options));

        var result = await runner.RunAsync("in");

        Assert.Equal(result.InvocationCount, invocationEndedWhenFlushed.Count);
        Assert.All(invocationEndedWhenFlushed, ended => Assert.True(ended));
        Assert.All(timeouts, t => Assert.Equal(TimeSpan.FromSeconds(1), t));
    }

    [Fact]
    public async Task AFailingFlush_DoesNotFailTheInvocation()
    {
        using var spans = new SpanCollector();
        var options = spans.ExecutionOptions(spans.Options(RemoteParent(), flush: (_, _) => throw new InvalidOperationException("exporter down")));
        await using var runner = new DurableTestRunner<string, string>(
            async (_, ctx) => await ctx.StepAsync((_, _) => Task.FromResult("v"), name: "s"), Runner(options));

        var result = await runner.RunAsync("in");

        result.EnsureSucceeded();
    }

    [Fact]
    public async Task UserCode_SeesTheMatchingSpanAsCurrent_AndTheCallersActivityIsRestored()
    {
        using var spans = new SpanCollector();
        string? inWorkflow = null, inStep = null, inChild = null, afterStep = null;

        var options = spans.ExecutionOptions(spans.Options(RemoteParent()));
        await using var runner = new DurableTestRunner<string, string>(async (_, ctx) =>
        {
            inWorkflow = Activity.Current?.OperationName;
            await ctx.RunInChildContextAsync(async (_, _) =>
            {
                inChild = Activity.Current?.OperationName;
                await Task.Yield();
                return 1;
            }, name: "outer");
            await ctx.StepAsync((_, _) => { inStep = Activity.Current?.OperationName; return Task.FromResult("v"); }, name: "charge");
            afterStep = Activity.Current?.OperationName;
            return "done";
        }, Runner(options));

        (await runner.RunAsync("in")).EnsureSucceeded();

        Assert.Equal("Invocation", inWorkflow);
        Assert.Equal("outer", inChild);
        Assert.Equal("charge attempt 1", inStep);
        Assert.Equal("Invocation", afterStep);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task TwoOtelFactories_AreRejectedBeforeAnythingRuns()
    {
        using var spans = new SpanCollector();
        var options = new DurableExecutionOptions();
        options.AddOpenTelemetry(OtelView.Invocation, spans.Options());
        options.AddOpenTelemetry(OtelView.Invocation, spans.Options());
        var ran = false;
        await using var runner = new DurableTestRunner<string, string>((_, _) => { ran = true; return Task.FromResult("x"); }, Runner(options));

        await Assert.ThrowsAsync<PluginLoadException>(() => runner.RunAsync("in"));

        Assert.False(ran);
        Assert.Empty(spans.Spans);
    }

    [Fact]
    public void ExecutionView_IsNotAvailableYet()
    {
        Assert.Throws<NotSupportedException>(() => OtelPlugin.CreateFactory(OtelView.Execution));
        Assert.Throws<NotSupportedException>(() => OtelPlugin.CreateFactory());
    }
}
