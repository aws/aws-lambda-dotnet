// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace Spectre.Console.Cli;

/// <summary>
/// Provides an abstract base class for asynchronous commands that support cancellation.
/// </summary>
/// <typeparam name="TSettings">The type of the settings used for the command.</typeparam>
public abstract class CancellableAsyncCommand<TSettings> : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    /// <summary>
    /// Executes the command asynchronously, with support for cancellation.
    /// </summary>
    public abstract Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationTokenSource cancellationTokenSource);

    /// <summary>
    /// Executes the command asynchronously with built-in cancellation handling.
    /// </summary>
    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        using var cancellationSource = new CancellationTokenSource();

        using var sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, onSignal);
        using var sigQuit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, onSignal);
        using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, onSignal);

        var cancellable = ExecuteAsync(context, settings, cancellationSource);
        return await cancellable;

        void onSignal(PosixSignalContext context)
        {
            context.Cancel = true;
            cancellationSource.Cancel();
        }
    }
}
