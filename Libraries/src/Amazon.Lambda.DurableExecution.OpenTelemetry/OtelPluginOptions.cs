// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>Settings for the OpenTelemetry tracing plugin.</summary>
public sealed class OtelPluginOptions
{
    /// <summary>The name of the <see cref="System.Diagnostics.ActivitySource"/> the spans come from.</summary>
    public string InstrumentationName { get; init; } = OtelPlugin.ActivitySourceName;

    /// <summary>The name of the Workflow span.</summary>
    public string WorkflowSpanName { get; init; } = "Workflow";

    /// <summary>
    /// Adds <c>otelTraceId</c>, <c>otelSpanId</c> and <c>otelTraceSampled</c> to the logging scope of durable log
    /// entries written while a span is current.
    /// </summary>
    public bool EnrichLogger { get; init; } = true;

    /// <summary>
    /// Called at the end of every invocation so the application can export spans before Lambda freezes the
    /// environment, for example by calling <c>TracerProvider.ForceFlush</c>. The plugin does not reference the
    /// OpenTelemetry SDK and cannot flush by itself. The token is cancelled when <see cref="FlushTimeout"/> or the
    /// invocation's remaining time runs out.
    /// </summary>
    public Func<TimeSpan, CancellationToken, Task>? FlushAsync { get; init; }

    /// <summary>The longest <see cref="FlushAsync"/> may take. Default: one second.</summary>
    public TimeSpan FlushTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Finds the trace context of the durable execution. Default: <see cref="ContextExtractors.XRay"/>, which reads
    /// <see cref="InvocationInfo.XRayTraceId"/>. A custom extractor must return the execution's own context: one
    /// that changes between invocations of the same execution splits it across traces.
    /// </summary>
    public Func<InvocationInfo, ExtractedContext?>? ContextExtractor { get; init; }
}

/// <summary>The upstream sampling decision carried by a trace header.</summary>
public enum UpstreamSampling
{
    /// <summary>No decision was made upstream. The application's sampler decides.</summary>
    Undecided,

    /// <summary>Upstream decided to sample.</summary>
    Sampled,

    /// <summary>Upstream decided not to sample.</summary>
    NotSampled,
}

/// <summary>A trace context found by a <see cref="OtelPluginOptions.ContextExtractor"/>.</summary>
public sealed class ExtractedContext
{
    /// <summary>The 32 lowercase hex characters of the trace id.</summary>
    public required string TraceId { get; init; }

    /// <summary>The 16 lowercase hex characters of the remote parent span, if the header carried one.</summary>
    public string? ParentSpanId { get; init; }

    /// <summary>The upstream sampling decision.</summary>
    public UpstreamSampling Sampling { get; init; }
}
