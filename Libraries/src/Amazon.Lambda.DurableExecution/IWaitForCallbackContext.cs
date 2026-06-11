// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Context passed to the submitter delegate of
/// <see cref="IDurableContext.WaitForCallbackAsync{T}(System.Func{string, IWaitForCallbackContext, System.Threading.CancellationToken, System.Threading.Tasks.Task}, string?, WaitForCallbackConfig?, System.Threading.CancellationToken)"/>.
/// Provides a replay-safe logger scoped to the submitter step.
/// </summary>
/// <remarks>
/// Distinct from <see cref="IStepContext"/> so the submitter API can evolve
/// independently. Logger-only surface.
/// </remarks>
public interface IWaitForCallbackContext
{
    /// <summary>
    /// Logger scoped to the submitter step.
    /// </summary>
    ILogger Logger { get; }
}
