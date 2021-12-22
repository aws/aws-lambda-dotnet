#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestServerlessApp.IntegrationTests.Helpers
{
    public static class CommandLineWrapper
    {
        public static async Task RunAsync(string command, string workingDirectory = "", bool redirectIo = true, CancellationToken cancelToken = default)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = GetSystemShell(),
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {command}" : $"-c \"{command}\"",

                RedirectStandardInput = redirectIo,
                RedirectStandardOutput = redirectIo,
                RedirectStandardError = redirectIo,
                UseShellExecute = false,
                CreateNoWindow = redirectIo,
                WorkingDirectory = workingDirectory
            };

            var process = Process.Start(processStartInfo);
            if (null == process)
                throw new Exception("Process.Start failed to return a non-null process");

            var sb = new StringBuilder();
            DataReceivedEventHandler callback = (object sender, DataReceivedEventArgs e) =>
            {
                sb.AppendLine(e.Data);
                Console.WriteLine(e.Data);
            };

            if (redirectIo)
            {
                process.OutputDataReceived += callback;
                process.ErrorDataReceived += callback;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            await process.WaitForExitAsync(cancelToken);
            var log = sb.ToString();
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

        private static bool TryGetEnvironmentVariable(string variable, out string? value)
        {
            value = Environment.GetEnvironmentVariable(variable);

            return !string.IsNullOrEmpty(value);
        }
    }
}