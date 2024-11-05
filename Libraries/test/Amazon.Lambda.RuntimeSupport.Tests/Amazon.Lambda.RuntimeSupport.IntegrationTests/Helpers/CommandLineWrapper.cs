using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;

public static class CommandLineWrapper
{
    public static void Run(string command, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
            
        using (var process = Process.Start(startInfo))
        {
            if (process == null)
                throw new Exception($"Unable to start process: {command} {arguments}");
            
            // Capture both output and error in a blocking way to ensure they are fully read
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            // Wait for the process to exit completely
            process.WaitForExit();

            // Output captured output and error to TestContext or Console
            TestContext.Progress.WriteLine(output);
            if (!string.IsNullOrWhiteSpace(error))
            {
                TestContext.Progress.WriteLine("Errors:");
                TestContext.Progress.WriteLine(error);
            }
                
            Assert.That(process.ExitCode == 0, $"Command '{command} {arguments}' failed.");
        }
    }

    private static string GetSystemShell()
    {
        if (TryGetEnvironmentVariable("COMSPEC", out var comspec))
            return comspec!;

        if (TryGetEnvironmentVariable("SHELL", out var shell))
            return shell!;

        // fall back to defaults
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "bash";
    }

    private static bool TryGetEnvironmentVariable(string variable, out string value)
    {
        value = Environment.GetEnvironmentVariable(variable);

        return !string.IsNullOrEmpty(value);
    }
}