// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Immutable view over a <see cref="CompletionConfig"/>'s thresholds that answers
/// the two questions a parallel run asks of its completion criteria:
/// <list type="number">
///   <item><see cref="ShouldStopDispatching"/> — mid-flight, may we stop launching
///       new branches because the run is already decided?</item>
///   <item><see cref="Evaluate"/> — once every dispatched branch has settled, what
///       is the final <see cref="CompletionReason"/>?</item>
/// </list>
/// Centralising the threshold arithmetic here keeps the "&gt; tolerated", "&gt;=
/// minimum" comparisons in one place rather than duplicated across the dispatch
/// loop and the post-settle verdict.
/// </summary>
internal readonly struct CompletionPolicy
{
    private readonly int? _minSuccessful;
    private readonly int? _toleratedFailureCount;
    private readonly double? _toleratedFailurePercentage;

    public CompletionPolicy(CompletionConfig config)
    {
        _minSuccessful = config.MinSuccessful;
        _toleratedFailureCount = config.ToleratedFailureCount;
        _toleratedFailurePercentage = config.ToleratedFailurePercentage;
    }

    /// <summary>
    /// Dispatch-loop short-circuit: stop launching new branches once the run is
    /// already decided — either enough branches have succeeded, or too many have
    /// failed. Reads slightly-stale counters by design (see the dispatch loop);
    /// <see cref="Evaluate"/> is the authoritative verdict.
    /// </summary>
    public bool ShouldStopDispatching(int succeeded, int failed, int totalBranches)
        => MinSuccessfulReached(succeeded) || FailureToleranceExceeded(failed, totalBranches);

    /// <summary>
    /// Final verdict once all dispatched branches have settled. Failure tolerance
    /// is checked first: exceeding it is terminal regardless of how many branches
    /// succeeded. <paramref name="started"/> counts branches that were never
    /// dispatched (non-zero only when a success short-circuit fired) — it
    /// distinguishes "hit the minimum and stopped early" from "everything ran".
    /// </summary>
    public CompletionReason Evaluate(int succeeded, int failed, int started, int totalBranches)
    {
        if (FailureToleranceExceeded(failed, totalBranches))
            return CompletionReason.FailureToleranceExceeded;

        // Min-successful satisfied AND we stopped early (some branch never ran).
        if (started > 0 && MinSuccessfulReached(succeeded))
            return CompletionReason.MinSuccessfulReached;

        return CompletionReason.AllCompleted;
    }

    // Enough wins to resolve successfully.
    private bool MinSuccessfulReached(int succeeded)
        => _minSuccessful is { } min && succeeded >= min;

    // Failure count or ratio STRICTLY exceeds a configured threshold. Only a
    // threshold that was explicitly set can trip this — an "empty" CompletionConfig
    // (all properties null) is permissive. CompletionConfig.AllSuccessful() opts
    // into fail-fast by setting ToleratedFailureCount = 0.
    private bool FailureToleranceExceeded(int failed, int totalBranches)
    {
        if (_toleratedFailureCount is { } tfc && failed > tfc)
            return true;

        if (_toleratedFailurePercentage is { } tfp && totalBranches > 0 &&
            (double)failed / totalBranches > tfp)
        {
            return true;
        }

        return false;
    }
}
