using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;

public static class CommandLineWrapper
{
    public static async Task Run(string command, string arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using (var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true })
        {
            var tcs = new TaskCompletionSource<bool>();

            // Handle process exit event
            process.Exited += (sender, args) =>
            {
                tcs.TrySetResult(true);
            };

            try
            {
                // Attach event handlers
                process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Console.WriteLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Console.WriteLine(args.Data);
                    }
                };

                // Start the process
                process.Start();

                // Begin asynchronous read operations
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the process to exit or cancellation
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cancellationToken));

                if (completedTask == tcs.Task)
                {
                    // Process exited normally
                    await tcs.Task; // Just to propagate any exceptions
                }
                else
                {
                    // Cancellation requested
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            
            Assert.True(process.ExitCode == 0, $"Command '{command} {arguments}' failed.");
        }
    }
}