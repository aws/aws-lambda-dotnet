// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>Where operation spans are parented.</summary>
public enum OtelView
{
    /// <summary>
    /// <c>Workflow -> operation -> attempt</c>. Operations and attempts link the Invocation span.
    /// Not implemented yet: <see cref="OtelPlugin.CreateFactory"/> throws <see cref="NotSupportedException"/>.
    /// </summary>
    Execution,

    /// <summary><c>Invocation -> operation -> attempt</c>. Operations and attempts link the Workflow span.</summary>
    Invocation,
}

/// <summary>Creates the OpenTelemetry tracing plugin for durable executions.</summary>
public static class OtelPlugin
{
    /// <summary>
    /// The default <see cref="System.Diagnostics.ActivitySource"/> name. Add it to the application's tracer provider
    /// (<c>AddSource</c>) so the spans are exported.
    /// </summary>
    public const string ActivitySourceName = "Amazon.Lambda.DurableExecution";

    /// <summary>The <see cref="IDurableInstrumentationPluginFactory.ExclusiveGroup"/> of both views.</summary>
    internal const string ViewExclusiveGroup = "durable-opentelemetry-view";

    /// <summary>
    /// Creates a plugin factory. Register exactly one, in <see cref="DurableExecutionOptions.Plugins"/> or with
    /// <see cref="DurableExecutionOptionsExtensions.AddOpenTelemetry"/>.
    /// </summary>
    /// <param name="view">Where operation spans are parented.</param>
    /// <param name="options">Optional settings.</param>
    /// <exception cref="NotSupportedException"><paramref name="view"/> is <see cref="OtelView.Execution"/>.</exception>
    public static IDurableInstrumentationPluginFactory CreateFactory(
        OtelView view = OtelView.Execution, OtelPluginOptions? options = null)
    {
        return view switch
        {
            OtelView.Invocation => new InvocationOtelPluginFactory(options ?? new OtelPluginOptions()),
            OtelView.Execution => throw new NotSupportedException(
                "OtelView.Execution is not implemented yet. Use OtelView.Invocation."),
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unknown view."),
        };
    }
}

/// <summary>Registration helpers for <see cref="DurableExecutionOptions"/>.</summary>
public static class DurableExecutionOptionsExtensions
{
    /// <summary>Adds the OpenTelemetry tracing plugin. Returns <paramref name="options"/> so calls can be chained.</summary>
    /// <param name="options">The options to add the plugin to.</param>
    /// <param name="view">Where operation spans are parented.</param>
    /// <param name="pluginOptions">Optional settings.</param>
    public static DurableExecutionOptions AddOpenTelemetry(
        this DurableExecutionOptions options, OtelView view = OtelView.Execution, OtelPluginOptions? pluginOptions = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Plugins.Add(OtelPlugin.CreateFactory(view, pluginOptions));
        return options;
    }
}
