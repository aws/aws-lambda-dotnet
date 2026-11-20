// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Instrumentation;

/// <summary>
/// Observes the lifecycle of one durable execution invocation, for example to emit traces or metrics.
/// A plugin instance serves a single invocation: the SDK creates it through
/// <see cref="IDurableInstrumentationPluginFactory.CreatePlugin"/> at the start of every invocation, so
/// per-invocation state can live in ordinary fields and concurrent invocations never share a plugin.
/// </summary>
/// <remarks>
/// <para>Every member is optional. An exception thrown by a hook is logged and ignored; it never fails the
/// invocation or the workflow.</para>
/// <para>The operation and attempt hooks are synchronous and the SDK does not wait for any work a plugin
/// starts from them, so a plugin cannot delay an operation or the decision to suspend the invocation.
/// Work that must finish before the response is returned belongs in <see cref="OnInvocationEndAsync"/>.</para>
/// <para>With several plugins, start hooks run in registration order and
/// <see cref="OnInvocationEndAsync"/> runs in reverse order. For <see cref="WrapAsync"/> the first
/// registered plugin is the outermost.</para>
/// </remarks>
public interface IDurableInstrumentationPlugin
{
    /// <summary>Called once, before any workflow code runs.</summary>
    void OnInvocationStart(InvocationInfo info) { }

    /// <summary>
    /// Called last, after the workflow outcome is decided and checkpoints are flushed. The SDK waits for the
    /// returned task, but only until shortly before the invocation's remaining time runs out; it then cancels
    /// <paramref name="cancellationToken"/> and continues without waiting.
    /// </summary>
    Task OnInvocationEndAsync(InvocationEndInfo info, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Called when an operation starts or is reached again during replay.</summary>
    void OnOperationStart(OperationInfo info) { }

    /// <summary>Called when an operation reaches a terminal state in this invocation or is replayed as terminal.</summary>
    void OnOperationEnd(OperationEndInfo info) { }

    /// <summary>Called before each execution of the user function of a step or similar operation.</summary>
    void OnAttemptStart(AttemptInfo info) { }

    /// <summary>Called after each execution of the user function, whether it succeeded or failed.</summary>
    void OnAttemptEnd(AttemptEndInfo info) { }

    /// <summary>
    /// Called when a checkpoint response changes the state of operations, for example when a callback is
    /// completed by an external system.
    /// </summary>
    void OnOperationChange(OperationChangeInfo info) { }

    /// <summary>
    /// Runs user code inside the plugin's own context, for example so an ambient activity is current while it
    /// executes. The plugin must call <paramref name="next"/> exactly once and await it. The SDK guarantees the
    /// user code runs even if the plugin throws or never calls <paramref name="next"/>, and an exception from the
    /// user code always reaches the caller unchanged.
    /// </summary>
    /// <remarks>
    /// When an invocation suspends, user code that is still running is abandoned and never completes, so code
    /// after the <c>await</c> of <paramref name="next"/> (including <c>finally</c> blocks) does not run for that
    /// work. Release anything held across <paramref name="next"/> from <see cref="OnInvocationEndAsync"/> too.
    /// </remarks>
    Task WrapAsync(UserCodeInfo info, Func<Task> next) => next();

    /// <summary>
    /// Called before a chained invoke is checkpointed. Returns an <c>X-Amzn-Trace-Id</c> header value that is
    /// sent to the invoked function, or null to contribute nothing. Not called when the invoke is replayed.
    /// </summary>
    string? ProvidePropagationMetadata(PropagationInput input) => null;

    /// <summary>
    /// Returns fields, such as a trace id, that the SDK adds to the logging scope of durable log entries.
    /// Called for the workflow's logger, so keep it fast. Return null to add nothing.
    /// </summary>
    IReadOnlyDictionary<string, object>? GetLogScope() => null;
}
