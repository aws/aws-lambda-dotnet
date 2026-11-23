// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Instrumentation;

/// <summary>
/// Registered once, typically at cold start, and asked for a new plugin at the start of every invocation.
/// The factory owns whatever outlives an invocation (exporters, resolved configuration); the plugin it
/// returns owns the state of one invocation.
/// </summary>
public interface IDurableInstrumentationPluginFactory
{
    /// <summary>
    /// Creates the plugin for one invocation, or returns null to skip this invocation. Always return a new
    /// instance; do not cache one. An exception is logged and the plugin is skipped.
    /// </summary>
    IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info);

    /// <summary>
    /// When non-null, at most one registered factory may use this value. Registering two factories with the
    /// same group throws <see cref="PluginLoadException"/> before any plugin is created.
    /// </summary>
    string? ExclusiveGroup => null;
}
