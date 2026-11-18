// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>
/// The Invocation view: operations are children of the Invocation that runs them.
/// </summary>
/// <remarks>
/// <code>
/// Execution ancestor (remote parent or synthetic root)
/// +-- Workflow            exported once, by the invocation that finishes the execution
/// +-- Invocation          one per Lambda invocation
///     +-- operation       links Workflow
///         +-- attempt     links Workflow
/// </code>
/// Spans are timed on the invocation's own clock. The first segment of an operation has an id derived from the
/// execution ARN and the operation id. An operation that is reached again after it finished is exported as a new
/// span that links that first segment, because nothing is stored between invocations.
/// </remarks>
internal sealed class InvocationOtelPlugin : OtelPluginBase
{
    // Open spans by operation id, and open attempt spans by "operationId:attempt".
    private readonly Dictionary<string, Activity> _operationSpans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Activity> _attemptSpans = new(StringComparer.Ordinal);

    internal InvocationOtelPlugin(ActivitySource source, OtelPluginOptions options, InvocationInfo info)
        : base(source, options, info)
    {
    }

    // ── Operations ──────────────────────────────────────────────────────────

    public override void OnOperationStart(OperationInfo info)
    {
        if (!Enabled || Finalized)
            return;

        lock (Gate)
        {
            if (info.IsReplay && _operationSpans.ContainsKey(info.Id))
                return;

            var tags = OperationTags(info);
            tags.Add(new("durable.operation.status", "STARTED"));

            Activity? span = null;
            if (!info.IsReplay)
            {
                // The first segment of the operation. Its id is derived, so later invocations can link it.
                span = Start(info.Name ?? info.Type, ParentContext(info.ParentId), tags, WorkflowLinks(), Clock.Now);
                ActivityIdOverride.TrySet(span, DeterministicIds.SpanIdFromOperationId(info.Id, ExecutionArn));
            }
            else if (info.Type is OperationTypes.Context or OperationTypes.Step)
            {
                // An operation reached again after an earlier invocation started or finished it. The original span
                // is not stored anywhere, so this is a new span that links the Workflow and the first segment.
                span = Start(info.Name ?? info.Type, ParentContext(info.ParentId), tags, ReplayLinks(info.Id), Clock.Now);
            }

            if (span != null)
                _operationSpans[info.Id] = span;
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

        if (Externals.ShouldSkip(info))
            return;

        if (_operationSpans.Remove(info.Id, out var span))
        {
            if (info.Status != null)
                span.SetTag("durable.operation.status", info.Status);
            if (IsAttemptOperation(info) && info.Attempt != null)
                span.SetTag("durable.attempt.number", info.Attempt.Value);

            SetOperationStatus(span, info);
            RecordError(span, info.Error, info.Exception, Clock.Now);
            End(span);
        }
        else if (!info.IsReplay || Externals.IsPending(info.Id))
        {
            // The operation finished in an invocation that did not start it (a callback completed from outside, a
            // wait that elapsed): export the segment this invocation saw.
            var tags = OperationTags(info);
            if (info.Status != null)
                tags.Add(new("durable.operation.status", info.Status));
            if (IsAttemptOperation(info) && info.Attempt != null)
                tags.Add(new("durable.attempt.number", info.Attempt.Value));

            var now = Clock.Now;
            var continuation = Start(info.Name ?? info.Type, ParentContext(info.ParentId), tags, ReplayLinks(info.Id), now);
            if (continuation != null)
            {
                SetOperationStatus(continuation, info);
                RecordError(continuation, info.Error, info.Exception, now);
                End(continuation, now);
            }
        }

        Externals.MarkExported(info);
    }

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
            var parent = _operationSpans.TryGetValue(info.Id, out var op) ? op.Context : ParentContext(null);
            var span = Start($"{info.Name ?? info.Type} attempt {attempt}", parent, tags, WorkflowLinks(), Clock.Now);
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

            span.SetTag("durable.attempt.outcome", info.Outcome.ToString().ToUpperInvariant());
            var now = Clock.Now;
            if (info.Outcome == AttemptOutcome.Failed)
            {
                span.SetStatus(ActivityStatusCode.Error, info.Exception?.Message ?? "Attempt failed");
                RecordError(span, null, info.Exception, now);
            }
            else
            {
                span.SetStatus(ActivityStatusCode.Ok);
            }
            End(span, now);
        }
    }

    // ── Invocation end ──────────────────────────────────────────────────────

    protected override void BeforeInvocationEnds(InvocationEndInfo info)
    {
        // Waits, invokes and callbacks that finished while suspended and that the workflow did not reach.
        lock (Gate)
        {
            foreach (var pending in Externals.Pending)
                FinishOperation(ToEndInfo(pending));
        }
    }

    protected override void AfterInvocationEnds(InvocationEndInfo info, DateTimeOffset end)
    {
        lock (Gate)
        {
            foreach (var span in _attemptSpans.Values)
                End(span, end);
            _attemptSpans.Clear();

            // Operations still open (a wait, a retry that suspended) keep the STARTED status they were created with.
            foreach (var span in _operationSpans.Values)
                End(span, end);
            _operationSpans.Clear();
        }
    }

    // ── User code ───────────────────────────────────────────────────────────

    /// <summary>Makes the right span the current <see cref="Activity"/> while user code runs, then restores it.</summary>
    public override async Task WrapAsync(UserCodeInfo info, Func<Task> next)
    {
        Activity? span = null;
        if (Enabled)
        {
            lock (Gate)
            {
                span = info.Kind switch
                {
                    UserCodeKind.Invocation => InvocationSpan,
                    UserCodeKind.ChildContext => info.Operation != null && _operationSpans.TryGetValue(info.Operation.Id, out var c) ? c : null,
                    UserCodeKind.Attempt => info.Operation != null && _attemptSpans.TryGetValue(AttemptKey(info.Operation.Id, info.Operation.Attempt ?? 1), out var a) ? a : null,
                    _ => null,
                };
            }
        }

        if (span == null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var previous = Activity.Current;
        Activity.Current = span;
        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private ActivityContext ParentContext(string? parentOperationId) =>
        parentOperationId != null && _operationSpans.TryGetValue(parentOperationId, out var parent)
            ? parent.Context
            : InvocationSpan?.Context ?? Ancestor;

    private List<ActivityLink> ReplayLinks(string operationId) => new()
    {
        new ActivityLink(OperationContext(operationId)),
        new ActivityLink(WorkflowContext),
    };
}
