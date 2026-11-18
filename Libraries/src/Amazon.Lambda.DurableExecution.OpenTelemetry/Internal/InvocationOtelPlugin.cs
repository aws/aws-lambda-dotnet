// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>
/// Traces one invocation of a durable execution. Every invocation of the execution joins one trace, anchored at the
/// execution ancestor found when it starts: the propagated remote parent when there is one, otherwise a synthetic
/// root whose id comes from the execution ARN.
/// </summary>
/// <remarks>
/// <code>
/// Execution ancestor (remote parent or synthetic root)
/// +-- Workflow            exported once, by the invocation that finishes the execution
/// +-- Invocation          one per Lambda invocation
///     +-- operation       links Workflow
///         +-- attempt
/// </code>
/// Spans are timed on the invocation's own clock. The first segment of an operation has an id derived from the
/// execution ARN and the operation id. An operation that is reached again after it finished is exported as a new
/// span that links that first segment, because nothing is stored between invocations.
/// </remarks>
internal sealed class InvocationOtelPlugin : IDurableInstrumentationPlugin
{
    private readonly ActivitySource _source;
    private readonly OtelPluginOptions _options;
    private readonly string _executionArn;
    private readonly DateTimeOffset? _reportedExecutionStart;
    private readonly InvocationClock _clock = new();
    private readonly object _gate = new();
    private readonly ExternalCompletions _externals = new();

    // Open spans by operation id, and open attempt spans by "operationId:attempt".
    private readonly Dictionary<string, Activity> _operationSpans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Activity> _attemptSpans = new(StringComparer.Ordinal);

    private bool _enabled;
    private bool _finalized;
    private bool _syntheticRootExported;
    private Activity? _invocationSpan;

    private string _traceId = string.Empty;
    private ActivityTraceFlags _flags;
    private ActivityContext _ancestor;
    private bool _ancestorIsRemote;
    private ActivityContext _workflowContext;

    internal InvocationOtelPlugin(ActivitySource source, OtelPluginOptions options, InvocationInfo info)
    {
        _source = source;
        _options = options;
        _executionArn = info.ExecutionArn;
        _reportedExecutionStart = info.ExecutionStartTimestamp;
    }

    private DateTimeOffset WorkflowStart => _reportedExecutionStart ?? _invocationStart;
    private DateTimeOffset _invocationStart;

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
            _enabled = false;
            End(_invocationSpan);
            _invocationSpan = null;
            throw;
        }
    }

    private void Initialize(InvocationInfo info)
    {
        _invocationStart = _clock.Now;

        var extracted = (_options.ContextExtractor ?? ContextExtractors.XRay)(info);
        var hasTraceId = extracted != null && DeterministicIds.IsValidTraceId(extracted.TraceId);
        _traceId = hasTraceId
            ? extracted!.TraceId
            : DeterministicIds.TraceIdFromArn(_executionArn, _reportedExecutionStart);

        // An upstream decision is authoritative. Without one the execution is sampled, and the application's
        // sampler still gets to decide for each span.
        var sampled = !hasTraceId || extracted!.Sampling != UpstreamSampling.NotSampled;
        _flags = sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

        var traceId = ActivityTraceId.CreateFromString(_traceId);
        if (hasTraceId && DeterministicIds.IsValidSpanId(extracted!.ParentSpanId))
        {
            _ancestor = new ActivityContext(traceId, ActivitySpanId.CreateFromString(extracted.ParentSpanId), _flags, isRemote: true);
            _ancestorIsRemote = true;
        }
        else
        {
            var rootId = DeterministicIds.ExecutionRootSpanId(_executionArn);
            _ancestor = new ActivityContext(traceId, ActivitySpanId.CreateFromString(rootId), _flags);
            _ancestorIsRemote = false;
        }

        _workflowContext = new ActivityContext(
            traceId, ActivitySpanId.CreateFromString(DeterministicIds.WorkflowSpanId(_executionArn)), _flags);

        // The Invocation span joins an ambient span of the same trace (the Lambda or ADOT handler span) so the
        // trace shows the Lambda call. Otherwise it hangs off the execution ancestor.
        var ambient = Activity.Current;
        var parent = _ancestorIsRemote && ambient != null && ambient.TraceId == traceId
            && (ambient.ActivityTraceFlags & ActivityTraceFlags.Recorded) == (_flags & ActivityTraceFlags.Recorded)
            ? ambient.Context
            : _ancestor;

        _invocationSpan = Start("Invocation", parent, new List<KeyValuePair<string, object?>>
        {
            new("durable.execution.arn", _executionArn),
            new("durable.invocation.first", info.IsFirstInvocation),
        }, links: null, _invocationStart);

        if (_reportedExecutionStart != null)
            ExportSyntheticRoot(_reportedExecutionStart.Value);

        _enabled = true;
        lock (_gate)
            _externals.Observe(info.UpdatedOperations, info.Operations);
    }

    public async Task OnInvocationEndAsync(InvocationEndInfo info, CancellationToken cancellationToken)
    {
        if (!_enabled)
            return;

        // Waits, invokes and callbacks that finished while suspended and that the workflow did not reach.
        FinishPendingExternals();

        var end = _clock.Now;
        EndOpenSpans(end);

        if (_invocationSpan != null)
        {
            _invocationSpan.SetTag("durable.invocation.status", info.Status.ToString().ToUpperInvariant());
            switch (info.Status)
            {
                case InvocationEndStatus.Failed:
                    _invocationSpan.SetStatus(ActivityStatusCode.Error, info.Error?.ErrorMessage ?? info.Exception?.Message ?? "Execution failed");
                    break;
                case InvocationEndStatus.Succeeded:
                case InvocationEndStatus.Pending:
                    _invocationSpan.SetStatus(ActivityStatusCode.Ok);
                    break;
                // Retrying: the invocation will be retried, so its status stays unset.
            }
            End(_invocationSpan, end);
        }

        if ((_flags & ActivityTraceFlags.Recorded) != 0
            && info.Status is InvocationEndStatus.Succeeded or InvocationEndStatus.Failed)
        {
            ExportWorkflow(info, end);
            ExportSyntheticRoot(_reportedExecutionStart ?? end);
        }

        _enabled = false;
        _finalized = true;
        lock (_gate)
            _externals.Clear();

        await FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ExportWorkflow(InvocationEndInfo info, DateTimeOffset end)
    {
        var workflow = Start(_options.WorkflowSpanName, _ancestor, new List<KeyValuePair<string, object?>>
        {
            new("durable.execution.arn", _executionArn),
        }, links: null, WorkflowStart);
        if (workflow == null)
            return;

        ActivityIdOverride.TrySet(workflow, _workflowContext.SpanId.ToHexString());
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
        if (_ancestorIsRemote || _syntheticRootExported || (_flags & ActivityTraceFlags.Recorded) == 0)
            return;

        // Started under a made-up parent so the sampler sees the execution's trace and flags, then made a root.
        var fakeParent = new ActivityContext(_ancestor.TraceId, ActivitySpanId.CreateRandom(), _flags);
        var root = Start("DurableExecutionRoot", fakeParent, new List<KeyValuePair<string, object?>>
        {
            new("durable.execution.synthetic_root", true),
            new("durable.execution.arn", _executionArn),
        }, links: null, at);
        if (root == null)
            return;

        ActivityIdOverride.TrySet(root, _ancestor.SpanId.ToHexString(), asRoot: true);
        _syntheticRootExported = true;
        End(root, at);
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        var flush = _options.FlushAsync;
        if (flush == null)
            return;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.FlushTimeout);
            await flush(_options.FlushTimeout, cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // A failing flush must not fail the invocation. The spans stay queued in the application's provider.
        }
    }

    // ── Operations ──────────────────────────────────────────────────────────

    public void OnOperationStart(OperationInfo info)
    {
        if (!_enabled || _finalized)
            return;

        lock (_gate)
        {
            if (info.IsReplay && _operationSpans.ContainsKey(info.Id))
                return;

            var tags = OperationTags(info);
            tags.Add(new("durable.operation.status", "STARTED"));

            Activity? span = null;
            if (!info.IsReplay)
            {
                // The first segment of the operation. Its id is derived, so later invocations can link it.
                span = Start(info.Name ?? info.Type, ParentContext(info.ParentId), tags, WorkflowLinks(), _clock.Now);
                ActivityIdOverride.TrySet(span, DeterministicIds.SpanIdFromOperationId(info.Id, _executionArn));
            }
            else if (info.Type is OperationTypes.Context or OperationTypes.Step)
            {
                // A finished step or child context reached again. The original span is not stored anywhere, so this
                // is a new span that links the Workflow and the operation's first segment.
                span = Start(info.Name ?? info.Type, ParentContext(info.ParentId), tags, ReplayLinks(info.Id), _clock.Now);
            }

            if (span != null)
                _operationSpans[info.Id] = span;
        }
    }

    public void OnOperationEnd(OperationEndInfo info)
    {
        if (!_enabled)
            return;

        lock (_gate)
            FinishOperation(info);
    }

    private void FinishOperation(OperationEndInfo info)
    {
        // Callbacks, waits and invokes inside a child context that finished while suspended end with their parent.
        if (info.Type == OperationTypes.Context && !info.IsReplay)
        {
            foreach (var child in _externals.Pending.Where(c => c.ParentId == info.Id))
                FinishOperation(ToEndInfo(child));
        }

        if (_externals.ShouldSkip(info))
            return;

        if (_operationSpans.Remove(info.Id, out var span))
        {
            if (info.Status != null)
                span.SetTag("durable.operation.status", info.Status);
            if (IsAttemptOperation(info) && info.Attempt != null)
                span.SetTag("durable.attempt.number", info.Attempt.Value);

            SetOperationStatus(span, info);
            RecordError(span, info.Error, info.Exception, _clock.Now);
            End(span);
        }
        else if (!info.IsReplay || _externals.IsPending(info.Id))
        {
            // The operation finished in an invocation that did not start it (a callback completed from outside, a
            // wait that elapsed): export the segment this invocation saw.
            var tags = OperationTags(info);
            if (info.Status != null)
                tags.Add(new("durable.operation.status", info.Status));
            if (IsAttemptOperation(info) && info.Attempt != null)
                tags.Add(new("durable.attempt.number", info.Attempt.Value));

            var now = _clock.Now;
            var continuation = Start(info.Name ?? info.Type, ParentContext(info.ParentId), tags, ReplayLinks(info.Id), now);
            if (continuation != null)
            {
                SetOperationStatus(continuation, info);
                RecordError(continuation, info.Error, info.Exception, now);
                End(continuation, now);
            }
        }

        _externals.MarkExported(info);
    }

    private static bool IsAttemptOperation(OperationInfo info) =>
        info.Type == OperationTypes.Step || info.SubType == OperationSubTypes.WaitForCondition;

    private static OperationEndInfo ToEndInfo(OperationInfo info) => new()
    {
        Id = info.Id, Name = info.Name, Type = info.Type, SubType = info.SubType, ParentId = info.ParentId,
        Status = info.Status, StartTimestamp = info.StartTimestamp, EndTimestamp = info.EndTimestamp,
        Attempt = info.Attempt, IsReplay = false,
    };

    private void FinishPendingExternals()
    {
        lock (_gate)
        {
            foreach (var pending in _externals.Pending)
                FinishOperation(ToEndInfo(pending));
        }
    }

    private static void SetOperationStatus(Activity span, OperationInfo info)
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

    // ── Attempts ────────────────────────────────────────────────────────────

    public void OnAttemptStart(AttemptInfo info)
    {
        if (!_enabled || _finalized)
            return;

        var attempt = info.Attempt ?? 1;
        var tags = OperationTags(info);
        tags.Add(new("durable.attempt.number", attempt));

        lock (_gate)
        {
            var parent = _operationSpans.TryGetValue(info.Id, out var op) ? op.Context : ParentContext(null);
            var span = Start($"{info.Name ?? info.Type} attempt {attempt}", parent, tags, WorkflowLinks(), _clock.Now);
            if (span != null)
                _attemptSpans[AttemptKey(info.Id, attempt)] = span;
        }
    }

    public void OnAttemptEnd(AttemptEndInfo info)
    {
        if (!_enabled)
            return;

        var attempt = info.Attempt ?? 1;
        lock (_gate)
        {
            if (!_attemptSpans.Remove(AttemptKey(info.Id, attempt), out var span))
                return;

            span.SetTag("durable.attempt.outcome", info.Outcome.ToString().ToUpperInvariant());
            var now = _clock.Now;
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

    public void OnOperationChange(OperationChangeInfo info)
    {
        if (!_enabled)
            return;

        lock (_gate)
            _externals.Observe(info.UpdatedOperations, info.Operations);
    }

    private static string AttemptKey(string operationId, int attempt) => $"{operationId}:{attempt}";

    // ── User code, propagation, logging ─────────────────────────────────────

    /// <summary>Makes the right span the current <see cref="Activity"/> while user code runs, then restores it.</summary>
    public async Task WrapAsync(UserCodeInfo info, Func<Task> next)
    {
        Activity? span = null;
        if (_enabled)
        {
            lock (_gate)
            {
                span = info.Kind switch
                {
                    UserCodeKind.Invocation => _invocationSpan,
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

    /// <summary>
    /// The header the invoked function continues the trace from: this execution's trace, with the calling
    /// operation's span as the parent. No span is created here.
    /// </summary>
    public string? ProvidePropagationMetadata(PropagationInput input)
    {
        if (!_enabled || _finalized || input.ExecutionArn != _executionArn)
            return null;

        var spanId = DeterministicIds.SpanIdFromOperationId(input.OperationId, _executionArn);
        var sampled = (_flags & ActivityTraceFlags.Recorded) != 0 ? "1" : "0";
        return $"Root=1-{_traceId[..8]}-{_traceId[8..]};Parent={spanId};Sampled={sampled}";
    }

    public IReadOnlyDictionary<string, object>? GetLogScope()
    {
        if (!_enabled || !_options.EnrichLogger)
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

    private ActivityContext ParentContext(string? parentOperationId) =>
        parentOperationId != null && _operationSpans.TryGetValue(parentOperationId, out var parent)
            ? parent.Context
            : _invocationSpan?.Context ?? _ancestor;

    private List<KeyValuePair<string, object?>> OperationTags(OperationInfo info)
    {
        var tags = new List<KeyValuePair<string, object?>>(7)
        {
            new("durable.execution.arn", _executionArn),
            new("durable.operation.id", info.Id),
            new("durable.operation.type", info.Type),
        };
        if (info.Name != null)
            tags.Add(new("durable.operation.name", info.Name));
        if (info.SubType != null)
            tags.Add(new("durable.operation.subtype", info.SubType));
        return tags;
    }

    private List<ActivityLink> WorkflowLinks() => new() { new ActivityLink(_workflowContext) };

    private List<ActivityLink> ReplayLinks(string operationId) => new()
    {
        new ActivityLink(new ActivityContext(
            _ancestor.TraceId,
            ActivitySpanId.CreateFromString(DeterministicIds.SpanIdFromOperationId(operationId, _executionArn)),
            _flags)),
        new ActivityLink(_workflowContext),
    };

    private void EndOpenSpans(DateTimeOffset end)
    {
        lock (_gate)
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

    /// <summary>Starts a span without leaving it as <see cref="Activity.Current"/> of the calling context.</summary>
    private Activity? Start(
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

    private void End(Activity? span, DateTimeOffset? at = null)
    {
        if (span == null)
            return;

        var previous = Activity.Current;
        try
        {
            span.SetEndTime((at ?? _clock.Now).UtcDateTime);
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
    private static void RecordError(Activity span, ErrorObject? error, Exception? exception, DateTimeOffset at)
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
