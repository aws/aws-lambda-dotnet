// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Workflow-scoped cancellation source. Cancels when the
/// <see cref="TerminationManager"/> resolves so abandoned user-<c>Func</c> bodies
/// (the WhenAny loser in <see cref="DurableExecutionHandler"/>) unwind via
/// <see cref="OperationCanceledException"/> instead of running to completion on
/// the threadpool while Lambda is mid-response.
/// </summary>
/// <remarks>
/// One instance per durable function invocation, constructed and disposed by
/// <see cref="DurableFunction"/>. Operation classes that invoke user
/// <c>Func</c>s build a per-call linked CTS combining the caller's token with
/// <see cref="Token"/> and pass the linked token into the user code.
/// <para>
/// Checkpoint writes, batcher flushes, and other SDK-internal work do NOT
/// observe this token: successful work must persist even when the workflow is
/// being torn down.
/// </para>
/// </remarks>
internal sealed class WorkflowCancellation : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public WorkflowCancellation(TerminationManager terminationManager)
    {
        terminationManager.TerminationTask.ContinueWith(
            static (_, state) =>
            {
                var cts = (CancellationTokenSource)state!;
                try { cts.Cancel(); }
                catch (ObjectDisposedException) { }
            },
            _cts,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public void Dispose() => _cts.Dispose();
}
