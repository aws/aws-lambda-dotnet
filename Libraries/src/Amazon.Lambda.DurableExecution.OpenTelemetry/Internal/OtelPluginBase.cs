// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>
/// What both views share: finding the execution's trace, the Invocation span, the Workflow span and synthetic root,
/// sampling, trace propagation, log correlation and the flush. A view decides how operations and attempts become
/// spans.
/// </summary>
/// <remarks>
/// Every invocation of an execution joins one trace, anchored at the execution ancestor found when it starts: the
/// propagated remote parent when there is one, otherwise a synthetic root whose id comes from the execution ARN.
/// The Workflow span, the synthetic root and the first segment of each operation have ids derived from the ARN, so a
/// span exported by one invocation can name one exported by another.
/// </remarks>
internal abstract class OtelPluginBase : IDurableInstrumentationPlugin
{
    private readonly ActivitySource _source;
    private bool _syntheticRootExported;
    private DateTimeOffset _invocationStart;

    protected readonly OtelPluginOptions Options;
    protected readonly string ExecutionArn;
    protected readonly DateTimeOffset? ReportedExecutionStart;
    protected readonly InvocationClock Clock = new();
    protected readonly object Gate = new();
    protected readonly ExternalCompletions Externals = new();

    protected bool Enabled;
    protected bool Finalized;
    protected Activity? InvocationSpan;

    protected string TraceId = string.Empty;
    protected ActivityTraceFlags Flags;
    protected ActivityContext Ancestor;
    protected bool AncestorIsRemote;
    protected ActivityContext WorkflowContext;

    protected OtelPluginBase(ActivitySource source, OtelPluginOptions options, InvocationInfo info)
    {
        _source = source;
        Options = options;
        ExecutionArn = info.ExecutionArn;
        ReportedExecutionStart = info.ExecutionStartTimestamp;
    }

    /// <summary>What the Workflow span and the synthetic root are backdated to.</summary>
    private DateTimeOffset WorkflowStart => ReportedExecutionStart ?? _invocationStart;

    // ── Invocation ──────────────────────────────────────────────────────────

    public void OnInvocationStart(InvocationInfo info)
    {
        // Nothing to trace when nobody listens. Spans are not created and every later hook returns early.
        if (!_source.HasListeners())
            return;

        try
        {
            Initialize(info);
        }
        catch
        {
            Enabled = false;
            End(InvocationSpan);
            InvocationSpan = null;
            throw;
        }
    }

    private void Initialize(InvocationInfo info)
    {
        _invocationStart = Clock.Now;

        var extracted = (Options.ContextExtractor ?? ContextExtractors.XRay)(info);
        var hasTraceId = extracted != null && DeterministicIds.IsValidTraceId(extracted.TraceId);
        TraceId = hasTraceId
            ? extracted!.TraceId
            : DeterministicIds.TraceIdFromArn(ExecutionArn, ReportedExecutionStart);

        // An upstream decision is authoritative. Without one the execution is sampled, and the application's
        // sampler still gets to decide for each span.
        var sampled = !hasTraceId || extracted!.Sampling != UpstreamSampling.NotSampled;
        Flags = sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

        var traceId = ActivityTraceId.CreateFromString(TraceId);
        if (hasTraceId && DeterministicIds.IsValidSpanId(extracted!.ParentSpanId))
        {
            Ancestor = new ActivityContext(traceId, ActivitySpanId.CreateFromString(extracted.ParentSpanId), Flags, isRemote: true);
            AncestorIsRemote = true;
        }
        else
        {
            var rootId = DeterministicIds.ExecutionRootSpanId(ExecutionArn);
            Ancestor = new ActivityContext(traceId, ActivitySpanId.CreateFromString(rootId), Flags);
            AncestorIsRemote = false;
        }

        WorkflowContext = new ActivityContext(
            traceId, ActivitySpanId.CreateFromString(DeterministicIds.WorkflowSpanId(ExecutionArn)), Flags);

        // The Invocation span joins an ambient span of the same trace (the Lambda or ADOT handler span) so the
        // trace shows the Lambda call. Otherwise it hangs off the execution ancestor.
        var ambient = Activity.Current;
        var parent = AncestorIsRemote && ambient != null && ambient.TraceId == traceId
            && (ambient.ActivityTraceFlags & ActivityTraceFlags.Recorded) == (Flags & ActivityTraceFlags.Recorded)
            ? ambient.Context
            : Ancestor;

        InvocationSpan = Start("Invocation", parent, new List<KeyValuePair<string, object?>>
        {
            new("durable.execution.arn", ExecutionArn),
            new("durable.invocation.first", info.IsFirstInvocation),
        }, links: null, _invocationStart);

        if (ReportedExecutionStart != null)
            ExportSyntheticRoot(ReportedExecutionStart.Value);

        Enabled = true;
        lock (Gate)
            Externals.Observe(info.UpdatedOperations, info.Operations);
    }

    /// <summary>Lets a view export or release what it holds before the invocation span ends.</summary>
    protected virtual void BeforeInvocationEnds(InvocationEndInfo info) { }

    /// <summary>When the invocation span ends. A view can push it later than the clock, to enclose what it observed.</summary>
    protected virtual DateTimeOffset InvocationEndTime() => Clock.Now;

    /// <summary>Called after the invocation span, the Workflow span and the synthetic root were ended.</summary>
    protected virtual void AfterInvocationEnds(InvocationEndInfo info, DateTimeOffset end) { }

    public async Task OnInvocationEndAsync(InvocationEndInfo info, CancellationToken cancellationToken)
    {
        if (!Enabled)
            return;

        BeforeInvocationEnds(info);
        var end = InvocationEndTime();

        if (InvocationSpan != null)
        {
            InvocationSpan.SetTag("durable.invocation.status", info.Status.ToString().ToUpperInvariant());
            switch (info.Status)
            {
                case InvocationEndStatus.Failed:
                    InvocationSpan.SetStatus(ActivityStatusCode.Error, info.Error?.ErrorMessage ?? info.Exception?.Message ?? "Execution failed");
                    break;
                case InvocationEndStatus.Succeeded:
                case InvocationEndStatus.Pending:
                    InvocationSpan.SetStatus(ActivityStatusCode.Ok);
                    break;
                // Retrying: the invocation will be retried, so its status stays unset.
            }
            End(InvocationSpan, end);
        }

        if ((Flags & ActivityTraceFlags.Recorded) != 0
            && info.Status is InvocationEndStatus.Succeeded or InvocationEndStatus.Failed)
        {
            ExportWorkflow(info, end);
            ExportSyntheticRoot(ReportedExecutionStart ?? end);
        }

        AfterInvocationEnds(info, end);

        Enabled = false;
        Finalized = true;
        lock (Gate)
            Externals.Clear();

        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ExportWorkflow(InvocationEndInfo info, DateTimeOffset end)
    {
        var workflow = Start(Options.WorkflowSpanName, Ancestor, new List<KeyValuePair<string, object?>>
        {
            new("durable.execution.arn", ExecutionArn),
        }, links: null, WorkflowStart);
        if (workflow == null)
            return;

        ActivityIdOverride.TrySet(workflow, WorkflowContext.SpanId.ToHexString());
        workflow.SetTag("durable.execution.status", info.Status.ToString().ToUpperInvariant());
        if (info.Status == InvocationEndStatus.Failed)
            workflow.SetStatus(ActivityStatusCode.Error, info.Error?.ErrorMessage ?? "Execution failed");
        else
            workflow.SetStatus(ActivityStatusCode.Ok);
        End(workflow, end);
    }

    /// <summary>
    /// With no propagated parent the execution hangs off a synthetic root. It is exported, once per invocation,
    /// with the same id each time, so the trace has a root even when an earlier invocation's copy was lost.
    /// Consumers deduplicate on the span id.
    /// </summary>
    private void ExportSyntheticRoot(DateTimeOffset at)
    {
        if (AncestorIsRemote || _syntheticRootExported || (Flags & ActivityTraceFlags.Recorded) == 0)
            return;

        // Started under a made-up parent so the sampler sees the execution's trace and flags, then made a root.
        var fakeParent = new ActivityContext(Ancestor.TraceId, ActivitySpanId.CreateRandom(), Flags);
        var root = Start("DurableExecutionRoot", fakeParent, new List<KeyValuePair<string, object?>>
        {
            new("durable.execution.synthetic_root", true),
            new("durable.execution.arn", ExecutionArn),
        }, links: null, at);
        if (root == null)
            return;

        ActivityIdOverride.TrySet(root, Ancestor.SpanId.ToHexString(), asRoot: true);
        _syntheticRootExported = true;
        End(root, at);
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        var flush = Options.FlushAsync;
        if (flush == null)
            return;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Options.FlushTimeout);
            await flush(Options.FlushTimeout, cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // A failing flush must not fail the invocation. The spans stay queued in the application's provider.
        }
    }

    // ── Hooks a view implements ─────────────────────────────────────────────

    public abstract void OnOperationStart(OperationInfo info);
    public abstract void OnOperationEnd(OperationEndInfo info);
    public abstract void OnAttemptStart(AttemptInfo info);
    public abstract void OnAttemptEnd(AttemptEndInfo info);
    public abstract Task WrapAsync(UserCodeInfo info, Func<Task> next);

    public virtual void OnOperationChange(OperationChangeInfo info)
    {
        if (!Enabled)
            return;

        lock (Gate)
            Externals.Observe(info.UpdatedOperations, info.Operations);
    }

    // ── Propagation and logging ─────────────────────────────────────────────

    /// <summary>
    /// The header the invoked function continues the trace from: this execution's trace, with the calling
    /// operation's span as the parent. No span is created here.
    /// </summary>
    public string? ProvidePropagationMetadata(PropagationInput input)
    {
        if (!Enabled || Finalized || input.ExecutionArn != ExecutionArn)
            return null;

        var spanId = DeterministicIds.SpanIdFromOperationId(input.OperationId, ExecutionArn);
        var sampled = (Flags & ActivityTraceFlags.Recorded) != 0 ? "1" : "0";
        return $"Root=1-{TraceId[..8]}-{TraceId[8..]};Parent={spanId};Sampled={sampled}";
    }

    public IReadOnlyDictionary<string, object>? GetLogScope()
    {
        if (!Enabled || !Options.EnrichLogger)
            return null;

        var current = Activity.Current;
        if (current == null)
            return null;

        return new Dictionary<string, object>(3)
        {
            ["otelTraceId"] = current.TraceId.ToHexString(),
            ["otelSpanId"] = current.SpanId.ToHexString(),
            ["otelTraceSampled"] = (current.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0,
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    protected List<KeyValuePair<string, object?>> OperationTags(OperationInfo info)
    {
        var tags = new List<KeyValuePair<string, object?>>(7)
        {
            new("durable.execution.arn", ExecutionArn),
            new("durable.operation.id", info.Id),
            new("durable.operation.type", info.Type),
        };
        if (info.Name != null)
            tags.Add(new("durable.operation.name", info.Name));
        if (info.SubType != null)
            tags.Add(new("durable.operation.subtype", info.SubType));
        return tags;
    }

    protected static bool IsAttemptOperation(OperationInfo info) =>
        info.Type == OperationTypes.Step || info.SubType == OperationSubTypes.WaitForCondition;

    protected static string AttemptKey(string operationId, int attempt) => $"{operationId}:{attempt}";

    protected static OperationEndInfo ToEndInfo(OperationInfo info) => new()
    {
        Id = info.Id, Name = info.Name, Type = info.Type, SubType = info.SubType, ParentId = info.ParentId,
        Status = info.Status, StartTimestamp = info.StartTimestamp, EndTimestamp = info.EndTimestamp,
        Attempt = info.Attempt, IsReplay = false,
    };

    /// <summary>The context of an operation's first segment, which every invocation derives the same way.</summary>
    protected ActivityContext OperationContext(string operationId) => new(
        Ancestor.TraceId,
        ActivitySpanId.CreateFromString(DeterministicIds.SpanIdFromOperationId(operationId, ExecutionArn)),
        Flags);

    protected List<ActivityLink> WorkflowLinks() => new() { new ActivityLink(WorkflowContext) };

    protected static void SetOperationStatus(Activity span, OperationInfo info)
    {
        var hasError = info is OperationEndInfo { Error: not null } or OperationEndInfo { Exception: not null };
        if (hasError || info.Status is OperationStatuses.Failed or OperationStatuses.TimedOut or OperationStatuses.Stopped)
        {
            var message = (info as OperationEndInfo)?.Error?.ErrorMessage
                ?? (info as OperationEndInfo)?.Exception?.Message
                ?? info.Status;
            span.SetStatus(ActivityStatusCode.Error, message);
        }
        else if (info.Status == OperationStatuses.Succeeded)
        {
            span.SetStatus(ActivityStatusCode.Ok);
        }
    }

    /// <summary>Starts a span without leaving it as <see cref="Activity.Current"/> of the calling context.</summary>
    protected Activity? Start(
        string name, ActivityContext parent, IEnumerable<KeyValuePair<string, object?>> tags,
        IEnumerable<ActivityLink>? links, DateTimeOffset start)
    {
        var previous = Activity.Current;
        try
        {
            return _source.StartActivity(name, ActivityKind.Internal, parent, tags, links, start);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    protected void End(Activity? span, DateTimeOffset? at = null)
    {
        if (span == null)
            return;

        var previous = Activity.Current;
        try
        {
            span.SetEndTime((at ?? Clock.Now).UtcDateTime);
            span.Stop();
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    /// <summary>
    /// Adds the standard <c>exception</c> event. A stored error type is used as is, including one raised by a
    /// function in another language. The event is only recorded when there are error details.
    /// </summary>
    protected static void RecordError(Activity span, ErrorObject? error, Exception? exception, DateTimeOffset at)
    {
        if (error == null && exception == null)
            return;

        var tags = new ActivityTagsCollection
        {
            ["exception.type"] = error?.ErrorType ?? exception?.GetType().FullName ?? "Error",
        };
        var message = error?.ErrorMessage ?? exception?.Message;
        if (message != null)
            tags["exception.message"] = message;
        var stack = error?.StackTrace != null ? string.Join("\n", error.StackTrace) : exception?.StackTrace;
        if (stack != null)
            tags["exception.stacktrace"] = stack;

        span.AddEvent(new ActivityEvent("exception", at, tags));
    }
}
