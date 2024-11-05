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
        string tempOutputFile = Path.GetTempFileName();
        var startInfo = new ProcessStartInfo
        {
            FileName = GetSystemShell(),
            Arguments = 
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 
                    $"/c {command} {arguments} > \"{tempOutputFile}\" 2>&1" : 
                    $"-c \"{command} {arguments} > '{tempOutputFile}' 2>&1\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
            CreateNoWindow = true
        };
            
        using (var process = Process.Start(startInfo))
        {
            if (process == null)
                throw new Exception($"Unable to start process: {command} {arguments}");
            
            process.WaitForExit();

            string output = File.ReadAllText(tempOutputFile);
            Console.WriteLine(output);
                
            Assert.That(process.ExitCode == 0, $"Command '{command} {arguments}' failed.");
        }
        File.Delete(tempOutputFile);
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