using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;

public static class CommandLineWrapper
{
    public static void Run(string command, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetSystemShell(),
            Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {command} {arguments}" : $"-c \"{command} {arguments}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
            
        using (var process = new Process())
        {
            process.StartInfo = startInfo;
            
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => 
                { TestContext.Progress.WriteLine(e.Data); });
            
            process.Start();

            // To avoid deadlocks, use an asynchronous read operation on at least one of the streams.  
            process.BeginOutputReadLine();
            string error = process.StandardError.ReadToEnd();  
            process.WaitForExit();
            TestContext.Progress.WriteLine(error);
            
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
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh";
    }

    private static bool TryGetEnvironmentVariable(string variable, out string value)
    {
        value = Environment.GetEnvironmentVariable(variable);

        return !string.IsNullOrEmpty(value);
    }
}