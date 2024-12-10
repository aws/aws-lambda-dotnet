using System.Runtime.InteropServices;

namespace Spectre.Console.Cli;

public abstract class CancellableAsyncCommand<TSettings> : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    public abstract Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationTokenSource cancellationTokenSource);

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