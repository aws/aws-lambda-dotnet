// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.LocalEmulation;

/// <summary>
/// Shared helpers for the local drive loops: seeding the top-level EXECUTION operation and
/// computing when a suspended workflow can next make progress. Both the testing package's
/// orchestrator and the Test Tool's driver use these so the semantics stay identical.
/// </summary>
internal static class DurableEmulationHelpers
{
    /// <summary>The operation id of the seeded top-level EXECUTION op.</summary>
    public const string ExecutionOperationId = "exec-0";

    /// <summary>
    /// Seeds the top-level EXECUTION operation carrying the user input payload. Idempotent:
    /// re-driving (e.g. a replay pass, or WaitForResultAsync after a callback) must not reset the
    /// op back to Started or clobber recorded state.
    /// </summary>
    public static void SeedExecution(InMemoryOperationStore store, string arn, string? inputPayload)
    {
        if (store.GetOperation(arn, ExecutionOperationId) is not null)
            return;

        store.Upsert(arn, new Operation
        {
            Id = ExecutionOperationId,
            Type = OperationTypes.Execution,
            Status = OperationStatuses.Started,
            ExecutionDetails = new ExecutionDetails { InputPayload = inputPayload }
        });
    }

    /// <summary>
    /// Computes how long to wait before the workflow can next make progress, based on the earliest
    /// future <c>ScheduledEndTimestamp</c> (pending WAIT) or <c>NextAttemptTimestamp</c> (pending
    /// STEP retry backoff) across all operations. Returns false when no operation carries a future
    /// resume time.
    /// </summary>
    public static bool TryGetNextResumeDelay(InMemoryOperationStore store, string arn, out TimeSpan delay)
        => TryGetNextResumeDelay(store.GetAllOperations(arn), out delay);

    /// <summary>
    /// Operation-list overload of <see cref="TryGetNextResumeDelay(InMemoryOperationStore, string, out TimeSpan)"/>
    /// for callers that hold a snapshot rather than the store (e.g. the Test Tool driver, which
    /// does not expose the underlying store).
    /// </summary>
    public static bool TryGetNextResumeDelay(IReadOnlyList<Operation> operations, out TimeSpan delay)
    {
        long? earliest = null;
        foreach (var op in operations)
        {
            long? resumeAt = op.Type switch
            {
                OperationTypes.Wait when op.Status == OperationStatuses.Started
                    => op.WaitDetails?.ScheduledEndTimestamp,
                OperationTypes.Step when op.Status == OperationStatuses.Pending
                    => op.StepDetails?.NextAttemptTimestamp,
                _ => null,
            };

            if (resumeAt is { } ts && (earliest is null || ts < earliest))
                earliest = ts;
        }

        if (earliest is null)
        {
            delay = TimeSpan.Zero;
            return false;
        }

        var deltaMs = earliest.Value - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        delay = deltaMs > 0 ? TimeSpan.FromMilliseconds(deltaMs) : TimeSpan.Zero;
        return true;
    }
}
