// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>
/// The Execution view: operations are children of the Workflow span, whichever invocation runs them.
/// </summary>
/// <remarks>
/// <code>
/// Execution ancestor (remote parent or synthetic root)
/// +-- Workflow            exported once, by the invocation that finishes the execution
/// |   +-- operation       exported once, by the invocation where it finishes; links Invocation
/// |       +-- attempt     exported by the invocation that ran it; links Invocation
/// +-- Invocation          one per Lambda invocation
/// </code>
/// An operation has one span, with an id derived from the execution ARN and the operation id. While it is open its
/// identity is only a context: nothing is exported, so nothing is left unended when an invocation suspends. The span
/// is created and ended together when the operation finishes, from the start and end times the service recorded.
/// Attempts are real spans in the invocation that runs them, parented on the operation's derived id. The Workflow
/// span is deferred the same way, to the invocation that finishes the execution.
/// </remarks>
internal sealed class ExecutionOtelPlugin : OtelPluginBase
{
    /// <summary>What was seen when an operation started, kept until it finishes.</summary>
    private sealed class OperationStart
    {
        internal string? Name;
        internal string? SubType;
        internal string? ParentId;
        internal DateTimeOffset Start;
    }

    // Operations that started and have not finished, by id. Their span does not exist yet.
    private readonly Dictionary<string, ActivityContext> _operationContexts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OperationStart> _operationStarts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Activity> _attemptSpans = new(StringComparer.Ordinal);

    // Child contexts exported in this invocation. A completion whose parent is one of them keeps that parent.
    private readonly HashSet<string> _recordedContextIds = new(StringComparer.Ordinal);

    private IReadOnlyDictionary<string, OperationInfo> _endOperations = new Dictionary<string, OperationInfo>();
    private DateTimeOffset? _latestObserved;

    internal ExecutionOtelPlugin(ActivitySource source, OtelPluginOptions options, InvocationInfo info)
        : base(source, options, info)
    {
    }

    // ── Time ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The invocation clock, never earlier than a time already seen. The service's timestamps are authoritative, so
    /// a local clock that reads a little behind them must not put a span before something it enclosed.
    /// </summary>
    private DateTimeOffset LogicalNow() => Observe(Clock.Now);

    private DateTimeOffset Observe(DateTimeOffset time)
    {
        if (_latestObserved == null || time > _latestObserved)
            _latestObserved = time;
        return time;
    }

    private static DateTimeOffset Earliest(DateTimeOffset? a, DateTimeOffset b) => a is { } x && x <= b ? x : b;

    // ── Operations ──────────────────────────────────────────────────────────

    public override void OnOperationStart(OperationInfo info)
    {
        if (!Enabled || Finalized)
            return;

        lock (Gate)
        {
            _operationContexts[info.Id] = OperationContext(info.Id);

            _operationStarts.TryGetValue(info.Id, out var existing);
            var start = Earliest(existing?.Start, info.StartTimestamp ?? LogicalNow());
            var parentId = info.ParentId ?? existing?.ParentId;
            _operationStarts[info.Id] = new OperationStart
            {
                Name = info.Name ?? existing?.Name,
                SubType = info.SubType ?? existing?.SubType,
                ParentId = parentId,
                Start = start,
            };
            ObserveAncestorStart(parentId, start);
        }
    }

    public override void OnOperationEnd(OperationEndInfo info)
    {
        if (!Enabled)
            return;

        lock (Gate)
            FinishOperation(info);
    }

    private void FinishOperation(OperationEndInfo info)
    {
        // Callbacks, waits and invokes inside a child context that finished while suspended end with their parent.
        if (info.Type == OperationTypes.Context && !info.IsReplay)
        {
            foreach (var child in Externals.Pending.Where(c => c.ParentId == info.Id))
                FinishOperation(ToEndInfo(child));
        }

        _operationStarts.Remove(info.Id, out var started);
        _operationContexts.Remove(info.Id);

        if (Externals.ShouldSkip(info))
            return;

        // An operation exports once, in the invocation where it finishes. Reaching it again by replay is not that
        // invocation. A wait, invoke or callback completion this invocation learned about is the exception.
        if (info.IsReplay && !ExternalCompletions.IsExternal(info))
            return;

        var name = info.Name ?? started?.Name;
        var subType = info.SubType ?? started?.SubType;
        var parentId = info.ParentId ?? started?.ParentId;

        var tags = OperationTags(info);
        if (name != null && info.Name == null)
            tags.Add(new("durable.operation.name", name));
        if (subType != null && info.SubType == null)
            tags.Add(new("durable.operation.subtype", subType));
        if (info.Status != null)
            tags.Add(new("durable.operation.status", info.Status));
        if ((info.Type == OperationTypes.Step || subType == OperationSubTypes.WaitForCondition) && info.Attempt != null)
            tags.Add(new("durable.attempt.number", info.Attempt.Value));

        // The earliest start seen, whether the service's or the local one, so the operation encloses its children.
        // With no start at all the local clock stands in, but never later than the service's end.
        DateTimeOffset start;
        if (started != null || info.StartTimestamp != null)
        {
            start = Earliest(started?.Start, info.StartTimestamp ?? started!.Start);
            if (info.StartTimestamp is { } serviceStart && serviceStart < start)
                start = serviceStart;
        }
        else
        {
            var now = LogicalNow();
            start = info.EndTimestamp is { } serviceEnd && serviceEnd < now ? serviceEnd : now;
        }
        ObserveAncestorStart(parentId, start);
        Observe(start);

        var span = Start(name ?? info.Type, ParentContextFor(parentId), tags, InvocationLinks(), start);
        if (span != null)
        {
            ActivityIdOverride.TrySet(span, DeterministicIds.SpanIdFromOperationId(info.Id, ExecutionArn));

            var end = info.EndTimestamp ?? LogicalNow();
            if (end < start)
                end = start;
            Observe(end);

            SetOperationStatus(span, info);
            RecordError(span, info.Error, info.Exception, end);
            if (info.Type == OperationTypes.Context && span.IsAllDataRequested)
                _recordedContextIds.Add(info.Id);
            End(span, end);
        }

        Externals.MarkExported(info);
    }

    /// <summary>
    /// The parent of an operation or attempt span: the parent operation's derived identity, or the Workflow's for a
    /// top-level operation. The parent need not be open or even reached in this invocation.
    /// </summary>
    private ActivityContext ParentContextFor(string? parentOperationId)
    {
        if (parentOperationId == null)
            return WorkflowContext;

        return _operationContexts.TryGetValue(parentOperationId, out var open)
            ? open
            : OperationContext(parentOperationId);
    }

    /// <summary>
    /// A child that started earlier than its parent's recorded start moves that start earlier, up the chain of
    /// parents that are still open, so a parent exported when it finishes encloses everything it contains.
    /// </summary>
    private void ObserveAncestorStart(string? parentId, DateTimeOffset start)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (parentId != null && visited.Add(parentId))
        {
            if (!_operationStarts.TryGetValue(parentId, out var parent) || !_operationContexts.ContainsKey(parentId))
                return;

            start = Earliest(parent.Start, start);
            parent.Start = start;
            parentId = parent.ParentId;
        }
    }

    private List<ActivityLink>? InvocationLinks() =>
        InvocationSpan != null ? new List<ActivityLink> { new(InvocationSpan.Context) } : null;

    // ── Attempts ────────────────────────────────────────────────────────────

    public override void OnAttemptStart(AttemptInfo info)
    {
        if (!Enabled || Finalized)
            return;

        var attempt = info.Attempt ?? 1;
        var tags = OperationTags(info);
        tags.Add(new("durable.attempt.number", attempt));

        lock (Gate)
        {
            var start = info.StartTimestamp ?? LogicalNow();
            _operationStarts.TryGetValue(info.Id, out var operation);
            var parentId = info.ParentId ?? operation?.ParentId;
            var earliest = Earliest(operation?.Start, start);
            _operationStarts[info.Id] = new OperationStart
            {
                Name = operation?.Name, SubType = operation?.SubType, ParentId = parentId, Start = earliest,
            };
            ObserveAncestorStart(parentId, earliest);

            var span = Start(
                $"{info.Name ?? info.Type} attempt {attempt}", ParentContextFor(info.Id), tags, InvocationLinks(), Observe(start));
            if (span != null)
                _attemptSpans[AttemptKey(info.Id, attempt)] = span;
        }
    }

    public override void OnAttemptEnd(AttemptEndInfo info)
    {
        if (!Enabled)
            return;

        var attempt = info.Attempt ?? 1;
        lock (Gate)
        {
            if (!_attemptSpans.Remove(AttemptKey(info.Id, attempt), out var span))
                return;

            var end = Observe(info.EndTimestamp ?? LogicalNow());
            span.SetTag("durable.attempt.outcome", info.Outcome.ToString().ToUpperInvariant());
            if (info.Outcome == AttemptOutcome.Failed)
            {
                span.SetStatus(ActivityStatusCode.Error, info.Exception?.Message ?? "Attempt failed");
                RecordError(span, null, info.Exception, end);
            }
            else
            {
                span.SetStatus(ActivityStatusCode.Ok);
            }
            End(span, end);
        }
    }

    public override void OnOperationChange(OperationChangeInfo info)
    {
        if (!Enabled)
            return;

        lock (Gate)
        {
            _endOperations = info.Operations;
            Externals.Observe(info.UpdatedOperations, info.Operations);
        }
    }

    // ── Invocation end ──────────────────────────────────────────────────────

    protected override void BeforeInvocationEnds(InvocationEndInfo info)
    {
        lock (Gate)
        {
            _endOperations = info.Operations;
            DrainCompletions(info, final: false);
        }
    }

    /// <summary>The invocation ends no earlier than any boundary it observed, so it encloses the spans it exported.</summary>
    protected override DateTimeOffset InvocationEndTime()
    {
        lock (Gate)
        {
            var now = LogicalNow();
            return _latestObserved is { } latest && latest > now ? latest : now;
        }
    }

    protected override void AfterInvocationEnds(InvocationEndInfo info, DateTimeOffset end)
    {
        lock (Gate)
        {
            // Completions that waited for an open parent are exported now, whether or not the parent finished.
            DrainCompletions(info, final: true);

            // An open operation exports nothing: it is exported by the invocation where it finishes. Attempts are
            // real spans and end here.
            foreach (var span in _attemptSpans.Values)
                End(span, end);
            _attemptSpans.Clear();
        }
    }

    /// <summary>
    /// Exports the waits, invokes and callbacks that completed while the execution was suspended and that the
    /// workflow did not reach. One whose parent is a child context that is still open waits for that context unless
    /// this is the last drain.
    /// </summary>
    private void DrainCompletions(InvocationEndInfo info, bool final)
    {
        var terminal = info.Status is InvocationEndStatus.Succeeded or InvocationEndStatus.Failed;
        foreach (var operation in Externals.Pending)
        {
            var openParent = HasOpenUnrecordedParent(operation);
            if (openParent && !final)
                continue;

            var recordedParent = operation.ParentId != null && _recordedContextIds.Contains(operation.ParentId);
            var parentId = !recordedParent && ((terminal && openParent) || HasAbandonedParent(operation.ParentId))
                ? null
                : operation.ParentId;

            var end = ToEndInfo(operation);
            FinishOperation(new OperationEndInfo
            {
                Id = end.Id, Name = end.Name, Type = end.Type, SubType = end.SubType, ParentId = parentId,
                Status = end.Status, StartTimestamp = end.StartTimestamp, EndTimestamp = end.EndTimestamp,
                Attempt = end.Attempt, IsReplay = false,
            });
        }
    }

    private bool HasOpenUnrecordedParent(OperationInfo operation) =>
        operation.ParentId != null
        && _operationContexts.ContainsKey(operation.ParentId)
        && !_recordedContextIds.Contains(operation.ParentId);

    /// <summary>
    /// True when the parent is a child context that never finished (it is still started or waiting) inside an
    /// ancestor that did, so nothing will ever export it and the completion is attached to the Workflow instead.
    /// </summary>
    private bool HasAbandonedParent(string? parentId)
    {
        if (parentId == null || !_endOperations.TryGetValue(parentId, out var parent)
            || parent.Type != OperationTypes.Context
            || parent.Status is not (OperationStatuses.Started or OperationStatuses.Ready or OperationStatuses.Pending))
            return false;

        var visited = new HashSet<string>(StringComparer.Ordinal) { parent.Id };
        var ancestorId = parent.ParentId;
        while (ancestorId != null && visited.Add(ancestorId))
        {
            if (!_endOperations.TryGetValue(ancestorId, out var ancestor) || ancestor.Type != OperationTypes.Context)
                return false;
            if (ExternalCompletions.IsFinished(ancestor.Status))
                return true;
            ancestorId = ancestor.ParentId;
        }
        return false;
    }

    // ── User code ───────────────────────────────────────────────────────────

    /// <summary>
    /// Makes the Workflow, the child context or the attempt the current <see cref="Activity"/> while user code runs,
    /// then restores the caller's. The Workflow and child contexts have no span yet, so a stand-in carrying their
    /// derived identity is current: spans the application starts inside parent onto the span exported later.
    /// </summary>
    public override async Task WrapAsync(UserCodeInfo info, Func<Task> next)
    {
        Activity? current = null;
        if (Enabled)
        {
            lock (Gate)
            {
                current = info.Kind switch
                {
                    UserCodeKind.Invocation => StandIn(WorkflowContext),
                    UserCodeKind.ChildContext => info.Operation != null ? StandIn(ParentContextFor(info.Operation.Id)) : null,
                    UserCodeKind.Attempt => info.Operation != null && _attemptSpans.TryGetValue(AttemptKey(info.Operation.Id, info.Operation.Attempt ?? 1), out var a) ? a : null,
                    _ => null,
                };
            }
        }

        if (current == null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var previous = Activity.Current;
        Activity.Current = current;
        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    /// <summary>
    /// An activity that carries a span identity and is never exported. It is not created by an
    /// <see cref="ActivitySource"/>, so no listener sees it.
    /// </summary>
    private static Activity? StandIn(ActivityContext identity)
    {
        var previous = Activity.Current;
        try
        {
            var standIn = new Activity("durable-context");
            standIn.SetIdFormat(ActivityIdFormat.W3C);
            standIn.SetParentId(identity.TraceId, identity.SpanId, identity.TraceFlags);
            standIn.Start();
            ActivityIdOverride.TrySet(standIn, identity.SpanId.ToHexString());
            return standIn;
        }
        finally
        {
            Activity.Current = previous;
        }
    }
}
