// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Internal;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Shared helpers for tests that exercise the suspend/terminate path.
/// </summary>
internal static class TerminationTestHelpers
{
    /// <summary>
    /// Waits for the suspend signal deterministically instead of a fixed delay, which races under
    /// CI thread-pool pressure (the original <c>Task.Delay</c> assumed the suspend happened within a
    /// fixed window, which isn't guaranteed). The suspend path trips
    /// <see cref="TerminationManager.Terminate"/>, which completes
    /// <see cref="TerminationManager.TerminationTask"/>. Bounded by a timeout so a genuine
    /// non-suspension fails fast at the following assert instead of hanging.
    /// </summary>
    public static Task WaitForTerminationAsync(this TerminationManager tm, int timeoutSeconds = 10) =>
        Task.WhenAny(tm.TerminationTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
}
