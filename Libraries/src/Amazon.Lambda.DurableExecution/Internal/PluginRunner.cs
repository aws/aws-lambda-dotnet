// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.DurableExecution.Instrumentation;
using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Dispatches lifecycle events to the plugins of one invocation. A plugin can never fail the invocation:
/// every hook exception is logged and dropped. The runner only exists when at least one plugin was created,
/// so callers hold a nullable reference and pay nothing when no plugin is registered.
/// </summary>
internal sealed class PluginRunner
{
    /// <summary>Time kept free after <c>OnInvocationEndAsync</c> so the response can still be returned.</summary>
    internal static readonly TimeSpan EndReserve = TimeSpan.FromMilliseconds(50);

    private readonly IDurableInstrumentationPlugin[] _plugins;
    private readonly ILogger _logger;
    private readonly Func<TimeSpan?> _remainingTime;

    private PluginRunner(IDurableInstrumentationPlugin[] plugins, ILogger logger, Func<TimeSpan?> remainingTime)
    {
        _plugins = plugins;
        _logger = logger;
        _remainingTime = remainingTime;
    }

    /// <summary>
    /// Validates the registration and creates one plugin per factory for this invocation.
    /// Returns null when there is nothing to run.
    /// </summary>
    /// <exception cref="PluginLoadException">A factory is null or two factories share an exclusive group.</exception>
    internal static PluginRunner? Create(
        DurableExecutionOptions? options,
        InvocationInfo info,
        ILogger logger,
        Func<TimeSpan?> remainingTime)
    {
        if (options == null || options.Plugins.Count == 0)
            return null;

        ValidateRegistration(options.Plugins);

        var plugins = new List<IDurableInstrumentationPlugin>(options.Plugins.Count);
        foreach (var factory in options.Plugins)
        {
            try
            {
                var plugin = factory.CreatePlugin(info);
                if (plugin != null)
                    plugins.Add(plugin);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Plugin factory {Factory} threw from CreatePlugin and is skipped for this invocation.",
                    factory.GetType().FullName);
            }
        }

        return plugins.Count == 0 ? null : new PluginRunner(plugins.ToArray(), logger, remainingTime);
    }

    private static void ValidateRegistration(IList<IDurableInstrumentationPluginFactory> factories)
    {
        Dictionary<string, IDurableInstrumentationPluginFactory>? groups = null;
        for (var i = 0; i < factories.Count; i++)
        {
            var factory = factories[i];
            if (factory == null)
                throw new PluginLoadException($"DurableExecutionOptions.Plugins[{i}] is null.");

            var group = factory.ExclusiveGroup;
            if (string.IsNullOrEmpty(group))
                continue;

            groups ??= new Dictionary<string, IDurableInstrumentationPluginFactory>(StringComparer.Ordinal);
            if (groups.TryGetValue(group, out var first))
            {
                throw new PluginLoadException(
                    $"Plugin factories '{first.GetType().FullName}' and '{factory.GetType().FullName}' are both in the " +
                    $"exclusive group '{group}'. Register only one of them.");
            }
            groups.Add(group, factory);
        }
    }

    internal void InvocationStart(InvocationInfo info) =>
        ForEach(nameof(IDurableInstrumentationPlugin.OnInvocationStart), p => p.OnInvocationStart(info));

    internal void OperationStart(OperationInfo info) =>
        ForEach(nameof(IDurableInstrumentationPlugin.OnOperationStart), p => p.OnOperationStart(info));

    internal void OperationEnd(OperationEndInfo info) =>
        ForEach(nameof(IDurableInstrumentationPlugin.OnOperationEnd), p => p.OnOperationEnd(info));

    internal void AttemptStart(AttemptInfo info) =>
        ForEach(nameof(IDurableInstrumentationPlugin.OnAttemptStart), p => p.OnAttemptStart(info));

    internal void AttemptEnd(AttemptEndInfo info) =>
        ForEach(nameof(IDurableInstrumentationPlugin.OnAttemptEnd), p => p.OnAttemptEnd(info));

    internal void OperationChange(OperationChangeInfo info) =>
        ForEach(nameof(IDurableInstrumentationPlugin.OnOperationChange), p => p.OnOperationChange(info));

    private void ForEach(string hook, Action<IDurableInstrumentationPlugin> call)
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                call(plugin);
            }
            catch (Exception ex)
            {
                LogHookFailure(plugin, hook, ex);
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="userCode"/> inside every plugin's <c>WrapAsync</c>, the first plugin outermost.
    /// The user code runs exactly once whatever the plugins do, and its exception, if any, is the one thrown.
    /// </summary>
    internal Task WrapAsync(UserCodeInfo info, Func<Task> userCode) => WrapFrom(0, info, userCode);

    private async Task WrapFrom(int index, UserCodeInfo info, Func<Task> userCode)
    {
        if (index == _plugins.Length)
        {
            await userCode().ConfigureAwait(false);
            return;
        }

        var plugin = _plugins[index];
        Task? inner = null;
        Task Next() => inner ??= WrapFrom(index + 1, info, userCode);

        try
        {
            await plugin.WrapAsync(info, Next).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (inner == null)
            {
                // The plugin failed before it called next, so the user code has not started. Run it unwrapped.
                LogHookFailure(plugin, nameof(IDurableInstrumentationPlugin.WrapAsync), ex);
                await Next().ConfigureAwait(false);
                return;
            }

            if (!inner.IsCompleted)
            {
                // The plugin threw while the user code is still running. Let the user code finish; the plugin's
                // exception is not the workflow's outcome.
                LogHookFailure(plugin, nameof(IDurableInstrumentationPlugin.WrapAsync), ex);
                await inner.ConfigureAwait(false);
                return;
            }

            if (inner.IsFaulted || inner.IsCanceled)
            {
                // The user code failed. Whatever the plugin threw (usually the same exception passing through),
                // the caller must see the user code's own exception.
                await inner.ConfigureAwait(false);
            }

            LogHookFailure(plugin, nameof(IDurableInstrumentationPlugin.WrapAsync), ex);
            return;
        }

        if (inner == null)
        {
            // The plugin returned without calling next. Skipping the user code would silently drop the workflow.
            _logger.LogWarning(
                "Plugin {Plugin} returned from WrapAsync without calling next. The user code is run anyway.",
                plugin.GetType().FullName);
            await Next().ConfigureAwait(false);
            return;
        }

        // A plugin that swallowed the user code's exception must not turn a failure into a success.
        await inner.ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the first non-blank header contributed by a plugin, in registration order. Later different values
    /// are logged and ignored.
    /// </summary>
    internal string? ProvidePropagationMetadata(PropagationInput input)
    {
        string? chosen = null;
        IDurableInstrumentationPlugin? chosenBy = null;
        foreach (var plugin in _plugins)
        {
            string? value;
            try
            {
                value = plugin.ProvidePropagationMetadata(input);
            }
            catch (Exception ex)
            {
                LogHookFailure(plugin, nameof(IDurableInstrumentationPlugin.ProvidePropagationMetadata), ex);
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (chosen == null)
            {
                chosen = value;
                chosenBy = plugin;
            }
            else if (!string.Equals(chosen, value, StringComparison.Ordinal))
            {
                // The header itself is not logged: it identifies a trace.
                _logger.LogWarning(
                    "Plugin {Plugin} contributed trace propagation metadata that differs from the one from {Winner}. It is ignored.",
                    plugin.GetType().FullName, chosenBy!.GetType().FullName);
            }
        }

        return chosen;
    }

    /// <summary>Merges the log scope fields of all plugins. A later plugin cannot replace an earlier plugin's key.</summary>
    internal IReadOnlyDictionary<string, object>? GetLogScope()
    {
        Dictionary<string, object>? merged = null;
        foreach (var plugin in _plugins)
        {
            IReadOnlyDictionary<string, object>? fields;
            try
            {
                fields = plugin.GetLogScope();
            }
            catch (Exception ex)
            {
                LogHookFailure(plugin, nameof(IDurableInstrumentationPlugin.GetLogScope), ex);
                continue;
            }

            if (fields == null || fields.Count == 0)
                continue;

            merged ??= new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kv in fields)
                merged.TryAdd(kv.Key, kv.Value);
        }

        return merged;
    }

    /// <summary>
    /// Awaits <c>OnInvocationEndAsync</c> of each plugin in reverse registration order, sharing one time budget:
    /// the remaining invocation time minus <see cref="EndReserve"/>. A plugin that is still running when the
    /// budget ends is cancelled and abandoned.
    /// </summary>
    internal async Task InvocationEndAsync(InvocationEndInfo info)
    {
        var remaining = ReadRemainingTime();
        var started = Stopwatch.GetTimestamp();

        for (var i = _plugins.Length - 1; i >= 0; i--)
        {
            var plugin = _plugins[i];
            using var cts = new CancellationTokenSource();

            Task work;
            try
            {
                work = plugin.OnInvocationEndAsync(info, cts.Token) ?? Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogHookFailure(plugin, nameof(IDurableInstrumentationPlugin.OnInvocationEndAsync), ex);
                continue;
            }

            try
            {
                if (remaining is null)
                {
                    await work.ConfigureAwait(false);
                    continue;
                }

                var budget = remaining.Value - EndReserve - Stopwatch.GetElapsedTime(started);
                if (budget < TimeSpan.Zero)
                    budget = TimeSpan.Zero;

                using var delayCts = new CancellationTokenSource();
                var winner = await Task.WhenAny(work, Task.Delay(budget, delayCts.Token)).ConfigureAwait(false);
                if (winner == work)
                {
                    delayCts.Cancel();
                    await work.ConfigureAwait(false);
                    continue;
                }

                cts.Cancel();
                _logger.LogWarning(
                    "Plugin {Plugin} did not finish OnInvocationEndAsync within the remaining invocation time and is abandoned.",
                    plugin.GetType().FullName);
                // Nothing else observes the abandoned task; keep its fault from surfacing as unobserved.
                _ = work.ContinueWith(
                    static t => _ = t.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                LogHookFailure(plugin, nameof(IDurableInstrumentationPlugin.OnInvocationEndAsync), ex);
            }
        }
    }

    private TimeSpan? ReadRemainingTime()
    {
        try
        {
            var value = _remainingTime();
            return value is { } v && v > TimeSpan.Zero ? v : null;
        }
        catch
        {
            // A context that cannot report its remaining time has no deadline we can honor.
            return null;
        }
    }

    private void LogHookFailure(IDurableInstrumentationPlugin plugin, string hook, Exception ex) =>
        _logger.LogWarning(ex, "Plugin {Plugin} threw from {Hook}. The exception is ignored.",
            plugin.GetType().FullName, hook);
}
