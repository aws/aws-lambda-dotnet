// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Instrumentation;

/// <summary>The plugin registration is invalid, for example a null factory or two factories in one exclusive group.</summary>
public class PluginLoadException : DurableExecutionException
{
    /// <summary>Creates an empty <see cref="PluginLoadException"/>.</summary>
    public PluginLoadException() { }
    /// <summary>Creates a <see cref="PluginLoadException"/> with the given message.</summary>
    public PluginLoadException(string message) : base(message) { }
    /// <summary>Creates a <see cref="PluginLoadException"/> wrapping an inner exception.</summary>
    public PluginLoadException(string message, Exception innerException) : base(message, innerException) { }
}
