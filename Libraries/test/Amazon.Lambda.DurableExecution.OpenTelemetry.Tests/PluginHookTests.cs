// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;
using Xunit;
using static Amazon.Lambda.DurableExecution.OpenTelemetry.Tests.SpanCollector;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry.Tests;

/// <summary>Calls the plugin's hooks directly, for cases the in-memory service cannot produce.</summary>
public class PluginHookTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123456789012:execution:direct";

    private static OperationInfo Op(string id, string type, string status, string? name = null, string? parent = null, string? subType = null, bool replay = false) =>
        new() { Id = id, Type = type, Status = status, Name = name, ParentId = parent, SubType = subType, IsReplay = replay };

    private static InvocationInfo Info(params OperationInfo[] updated) => new()
    {
        ExecutionArn = Arn,
        RequestId = "req-1",
        IsFirstInvocation = false,
        UpdatedOperations = updated.ToDictionary(o => o.Id),
        Operations = updated.ToDictionary(o => o.Id),
    };

    private static OperationEndInfo End(OperationInfo op, bool replay) => new()
    {
        Id = op.Id, Type = op.Type, Name = op.Name, ParentId = op.ParentId, SubType = op.SubType,
        Status = op.Status, IsReplay = replay,
    };

    private static IDurableInstrumentationPlugin Start(SpanCollector spans, InvocationInfo info)
    {
        var factory = OtelPlugin.CreateFactory(OtelView.Invocation, spans.Options(RemoteParent()));
        var plugin = factory.CreatePlugin(info)!;
        plugin.OnInvocationStart(info);
        return plugin;
    }

    private static Task Finish(IDurableInstrumentationPlugin plugin, InvocationEndStatus status = InvocationEndStatus.Pending) =>
        plugin.OnInvocationEndAsync(new InvocationEndInfo { ExecutionArn = Arn, Status = status }, CancellationToken.None);

    [Fact]
    public async Task WaitThatElapsedWhileSuspended_IsExportedOnceInTheInvocationThatReachesIt()
    {
        using var spans = new SpanCollector();
        var wait = Op("wait-1", OperationTypes.Wait, OperationStatuses.Succeeded, "pause");
        var plugin = Start(spans, Info(wait));

        // The workflow reaches the finished wait again: a replay.
        plugin.OnOperationStart(Op("wait-1", OperationTypes.Wait, OperationStatuses.Succeeded, "pause", replay: true));
        plugin.OnOperationEnd(End(wait, replay: true));
        await Finish(plugin);

        var pause = Assert.Single(spans.All("pause"));
        Assert.Equal(spans.Single("Invocation").SpanId, pause.ParentSpanId);
        Assert.Equal("SUCCEEDED", Tag(pause, "durable.operation.status"));
        Assert.Equal(ActivityStatusCode.Ok, pause.Status);
        // It links the first segment of the wait (started by an earlier invocation) and the Workflow span.
        Assert.Equal(
            new[] { DeterministicIds.SpanIdFromOperationId("wait-1", Arn) },
            pause.Links.Select(l => l.Context.SpanId.ToHexString()).Take(1));
        Assert.Equal(2, pause.Links.Count());
    }

    [Fact]
    public async Task CompletionTheWorkflowDidNotReach_IsExportedWhenTheInvocationEnds_WithItsParent()
    {
        using var spans = new SpanCollector();
        var callback = Op("cb-1", OperationTypes.Callback, OperationStatuses.Succeeded, name: null, parent: "ctx-1");
        var parent = Op("ctx-1", OperationTypes.Context, OperationStatuses.Started, "approve", subType: OperationSubTypes.WaitForCallback);
        var info = new InvocationInfo
        {
            ExecutionArn = Arn,
            UpdatedOperations = new Dictionary<string, OperationInfo> { [callback.Id] = callback },
            Operations = new Dictionary<string, OperationInfo> { [callback.Id] = callback, [parent.Id] = parent },
        };
        var plugin = OtelPlugin.CreateFactory(OtelView.Invocation, spans.Options(RemoteParent())).CreatePlugin(info)!;
        plugin.OnInvocationStart(info);

        await Finish(plugin);

        // A wait-for-callback's callback has no name of its own, so it is named after its parent.
        var span = Assert.Single(spans.All("approve-callback"));
        Assert.Equal("CALLBACK", Tag(span, "durable.operation.type"));
        Assert.Equal("SUCCEEDED", Tag(span, "durable.operation.status"));
    }

    [Fact]
    public async Task ACompletionThatWasExported_IsNotExportedAgain()
    {
        using var spans = new SpanCollector();
        var wait = Op("wait-1", OperationTypes.Wait, OperationStatuses.Succeeded, "pause");
        var plugin = Start(spans, Info(wait));
        plugin.OnOperationEnd(End(wait, replay: true));

        plugin.OnOperationEnd(End(wait, replay: false));
        await Finish(plugin);

        Assert.Single(spans.All("pause"));
    }

    [Fact]
    public async Task ReplayedWaitWithNoCompletionNews_IsNotExported()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var wait = Op("wait-1", OperationTypes.Wait, OperationStatuses.Succeeded, "pause");

        plugin.OnOperationStart(Op("wait-1", OperationTypes.Wait, OperationStatuses.Succeeded, "pause", replay: true));
        plugin.OnOperationEnd(End(wait, replay: true));
        await Finish(plugin);

        Assert.Empty(spans.All("pause"));
    }

    [Fact]
    public async Task OperationChange_MakesALaterCompletionExportable()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var callback = Op("cb-1", OperationTypes.Callback, OperationStatuses.Failed, "approval");

        plugin.OnOperationChange(new OperationChangeInfo
        {
            ExecutionArn = Arn,
            UpdatedOperations = new Dictionary<string, OperationInfo> { [callback.Id] = callback },
            Operations = new Dictionary<string, OperationInfo> { [callback.Id] = callback },
        });
        await Finish(plugin);

        var span = Assert.Single(spans.All("approval"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public async Task ErrorDetails_UseTheStoredErrorTypeVerbatim()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var op = Op("step-1", OperationTypes.Step, OperationStatuses.Failed, "charge");
        plugin.OnOperationStart(op);

        plugin.OnOperationEnd(new OperationEndInfo
        {
            Id = op.Id, Type = op.Type, Name = op.Name, Status = OperationStatuses.Failed, Attempt = 2,
            Error = new ErrorObject { ErrorType = "PaymentDeclined", ErrorMessage = "no funds", StackTrace = new[] { "at A", "at B" } },
        });
        await Finish(plugin);

        var charge = spans.Single("charge");
        var exception = Assert.Single(charge.Events);
        Assert.Equal("PaymentDeclined", exception.Tags.Single(t => t.Key == "exception.type").Value);
        Assert.Equal("at A\nat B", exception.Tags.Single(t => t.Key == "exception.stacktrace").Value);
        Assert.Equal("2", Tag(charge, "durable.attempt.number"));
        Assert.Equal("no funds", charge.StatusDescription);
    }

    [Theory]
    [InlineData(OperationStatuses.Failed)]
    [InlineData(OperationStatuses.TimedOut)]
    [InlineData(OperationStatuses.Stopped)]
    public async Task FailureStatuses_AreErrorsEvenWithoutErrorDetails(string status)
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        var op = Op("cb-1", OperationTypes.Callback, OperationStatuses.Started, "approval");
        plugin.OnOperationStart(op);

        plugin.OnOperationEnd(End(Op("cb-1", OperationTypes.Callback, status, "approval"), replay: false));
        await Finish(plugin);

        var span = spans.Single("approval");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(status, span.StatusDescription);
        Assert.Empty(span.Events);
    }

    [Fact]
    public async Task SuspendingInvocation_EndsOpenSpansAsStarted()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        plugin.OnOperationStart(Op("wait-1", OperationTypes.Wait, OperationStatuses.Started, "pause"));
        plugin.OnOperationStart(Op("step-1", OperationTypes.Step, OperationStatuses.Started, "retrying"));
        plugin.OnAttemptStart(new AttemptInfo { Id = "step-1", Type = OperationTypes.Step, Name = "retrying", Attempt = 1 });

        await Finish(plugin);

        Assert.Equal("STARTED", Tag(spans.Single("pause"), "durable.operation.status"));
        Assert.Equal("STARTED", Tag(spans.Single("retrying"), "durable.operation.status"));
        Assert.Single(spans.All("retrying attempt 1"));
        Assert.Equal("PENDING", Tag(spans.Single("Invocation"), "durable.invocation.status"));
        // The execution is not finished, so there is no Workflow span.
        Assert.Empty(spans.All("Workflow"));
    }

    [Fact]
    public async Task RetryingInvocation_LeavesItsStatusUnset()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());

        await Finish(plugin, InvocationEndStatus.Retrying);

        var invocation = spans.Single("Invocation");
        Assert.Equal("RETRYING", Tag(invocation, "durable.invocation.status"));
        Assert.Equal(ActivityStatusCode.Unset, invocation.Status);
        Assert.Empty(spans.All("Workflow"));
    }

    [Fact]
    public async Task PropagationHeader_ContinuesTheTraceFromTheCallingOperation()
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
        Assert.Null(plugin.ProvidePropagationMetadata(new PropagationInput
        {
            ExecutionArn = "arn:other", OperationId = "invoke-1", TargetFunctionName = "other",
        }));
        await Finish(plugin);
        // After the invocation ended there is nothing to continue.
        Assert.Null(plugin.ProvidePropagationMetadata(new PropagationInput
        {
            ExecutionArn = Arn, OperationId = "invoke-1", TargetFunctionName = "other",
        }));
    }

    [Fact]
    public async Task PropagationHeader_CarriesTheUpstreamDecisionNotToSample()
    {
        using var spans = new SpanCollector();
        var factory = OtelPlugin.CreateFactory(OtelView.Invocation, spans.Options(RemoteParent(UpstreamSampling.NotSampled)));
        var info = Info();
        var plugin = factory.CreatePlugin(info)!;
        plugin.OnInvocationStart(info);

        var header = plugin.ProvidePropagationMetadata(new PropagationInput { ExecutionArn = Arn, OperationId = "i", TargetFunctionName = "f" });

        Assert.EndsWith(";Sampled=0", header);
        await Finish(plugin);
    }

    [Fact]
    public async Task LogScope_ReportsTheCurrentSpan_AndCanBeSwitchedOff()
    {
        using var spans = new SpanCollector();
        var plugin = Start(spans, Info());
        Assert.Null(plugin.GetLogScope()); // no span is current

        using var source = new ActivitySource(spans.SourceName);
        using var current = source.StartActivity("user span");
        var scope = plugin.GetLogScope();
        Assert.NotNull(scope);
        Assert.Equal(current!.TraceId.ToHexString(), scope!["otelTraceId"]);
        Assert.Equal(current.SpanId.ToHexString(), scope["otelSpanId"]);
        Assert.Equal(true, scope["otelTraceSampled"]);
        await Finish(plugin);

        var off = OtelPlugin.CreateFactory(OtelView.Invocation, new OtelPluginOptions
        {
            InstrumentationName = spans.SourceName, EnrichLogger = false, ContextExtractor = RemoteParent(),
        });
        var info = Info();
        var disabled = off.CreatePlugin(info)!;
        disabled.OnInvocationStart(info);
        Assert.Null(disabled.GetLogScope());
        await Finish(disabled);
    }

    [Fact]
    public async Task Hooks_BeforeTheInvocationStarted_Do_Nothing()
    {
        using var spans = new SpanCollector();
        var info = Info();
        var plugin = OtelPlugin.CreateFactory(OtelView.Invocation, spans.Options(RemoteParent())).CreatePlugin(info)!;

        plugin.OnOperationStart(Op("a", OperationTypes.Step, OperationStatuses.Started, "x"));
        plugin.OnOperationEnd(End(Op("a", OperationTypes.Step, OperationStatuses.Succeeded, "x"), replay: false));
        await Finish(plugin);

        Assert.Empty(spans.Spans);
    }
}
