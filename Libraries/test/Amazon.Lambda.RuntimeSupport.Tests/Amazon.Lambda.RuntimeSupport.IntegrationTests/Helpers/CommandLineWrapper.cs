using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;

public static class CommandLineWrapper
{
    public static async Task Run(string command, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetSystemShell(),
            Arguments = 
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 
                    $"/c {command} {arguments}" : 
                    $"-c \"{command} {arguments}\"",
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
            
            DataReceivedEventHandler callback = (object sender, DataReceivedEventArgs e) =>
            {
                TestContext.Progress.WriteLine(e.Data);
            };
            
            process.OutputDataReceived += callback;
            process.ErrorDataReceived += callback;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await process.WaitForExitAsync();
                
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