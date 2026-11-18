// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>Creates one <see cref="InvocationOtelPlugin"/> per invocation, all sharing one activity source.</summary>
internal sealed class InvocationOtelPluginFactory : IDurableInstrumentationPluginFactory
{
    private readonly OtelPluginOptions _options;
    private readonly ActivitySource _source;

    internal InvocationOtelPluginFactory(OtelPluginOptions options)
    {
        _options = options;
        _source = new ActivitySource(options.InstrumentationName);
    }

    public string? ExclusiveGroup => OtelPlugin.ViewExclusiveGroup;

    public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) =>
        new InvocationOtelPlugin(_source, _options, info);
}
