// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>Creates one plugin per invocation, in the chosen view, all sharing one activity source.</summary>
internal sealed class OtelPluginFactory : IDurableInstrumentationPluginFactory
{
    private readonly OtelView _view;
    private readonly OtelPluginOptions _options;
    private readonly ActivitySource _source;

    internal OtelPluginFactory(OtelView view, OtelPluginOptions options)
    {
        _view = view;
        _options = options;
        _source = new ActivitySource(options.InstrumentationName);
    }

    public string? ExclusiveGroup => OtelPlugin.ViewExclusiveGroup;

    public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) => _view switch
    {
        OtelView.Invocation => new InvocationOtelPlugin(_source, _options, info),
        OtelView.Execution => new ExecutionOtelPlugin(_source, _options, info),
        _ => throw new InvalidOperationException($"Unknown view '{_view}'."),
    };
}
