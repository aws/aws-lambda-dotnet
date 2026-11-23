// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Optional settings passed to <c>DurableFunction.WrapAsync</c>.
/// Create it once, for example in a static field, and reuse it for every invocation.
/// </summary>
public sealed class DurableExecutionOptions
{
    /// <summary>
    /// Instrumentation plugin factories, in registration order. The SDK asks each factory for a new plugin on
    /// every invocation.
    /// </summary>
    public IList<IDurableInstrumentationPluginFactory> Plugins { get; } = new List<IDurableInstrumentationPluginFactory>();
}
