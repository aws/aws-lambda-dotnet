// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.DurableExecution.Testing;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry.Tests;

/// <summary>
/// Collects the spans the plugin exports. The sampler defaults to the OpenTelemetry SDK's default,
/// ParentBased(AlwaysOn): a span follows the sampled flag of its parent, and a span without one is sampled.
/// </summary>
internal sealed class SpanCollector : IDisposable
{
    // Each test uses a distinct source name so tests running in parallel do not see each other's spans.
    internal string SourceName { get; } = "test-" + Guid.NewGuid().ToString("N");

    private readonly ActivityListener _listener;
    private readonly object _lock = new();
    private readonly List<Activity> _stopped = new();

    /// <summary>A sampler: given a span's name and parent, whether to record it.</summary>
    internal delegate bool SamplerFunc(string name, ActivityContext parent);

    /// <summary>The OpenTelemetry SDK's default, ParentBased(AlwaysOn).</summary>
    internal static readonly SamplerFunc ParentBased = (_, parent) =>
        parent == default || (parent.TraceFlags & ActivityTraceFlags.Recorded) != 0;

    /// <summary>AlwaysOn, which ignores the parent's sampled flag (<c>OTEL_TRACES_SAMPLER=always_on</c>).</summary>
    internal static readonly SamplerFunc AlwaysOn = (_, _) => true;

    internal SpanCollector(SamplerFunc? sampler = null)
    {
        var sample = sampler ?? ParentBased;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                sample(options.Name, options.Parent)
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.None,
            ActivityStopped = activity => { lock (_lock) _stopped.Add(activity); },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    internal IReadOnlyList<Activity> Spans
    {
        get { lock (_lock) return _stopped.ToList(); }
    }

    internal Activity Single(string name) => Spans.Single(s => s.OperationName == name);

    internal IReadOnlyList<Activity> All(string name) => Spans.Where(s => s.OperationName == name).ToList();

    internal OtelPluginOptions Options(
        Func<InvocationInfo, ExtractedContext?>? extractor = null, Func<TimeSpan, CancellationToken, Task>? flush = null) => new()
    {
        InstrumentationName = SourceName,
        ContextExtractor = extractor ?? (_ => null),
        FlushAsync = flush,
    };

    internal DurableExecutionOptions ExecutionOptions(OtelPluginOptions options, OtelView view = OtelView.Invocation)
    {
        var executionOptions = new DurableExecutionOptions();
        executionOptions.AddOpenTelemetry(view, options);
        return executionOptions;
    }

    public void Dispose() => _listener.Dispose();

    // Fixed upstream context used by the tests that need a remote parent.
    internal const string RemoteTraceId = "5759e988bd862e3fe1be46a994272793";
    internal const string RemoteParentSpanId = "53995c3f42cd8ad8";

    internal static Func<InvocationInfo, ExtractedContext?> RemoteParent(UpstreamSampling sampling = UpstreamSampling.Sampled) =>
        _ => new ExtractedContext { TraceId = RemoteTraceId, ParentSpanId = RemoteParentSpanId, Sampling = sampling };

    internal static TestRunnerOptions Runner(DurableExecutionOptions options) => new() { ExecutionOptions = options };

    internal static string Tag(Activity span, string key) =>
        span.TagObjects.FirstOrDefault(t => t.Key == key).Value?.ToString() ?? string.Empty;

    internal static bool HasTag(Activity span, string key) => span.TagObjects.Any(t => t.Key == key);
}
