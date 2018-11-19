using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Amazon.Lambda.TestTool.ExternalCommands
{
    /// <summary>
    /// This class handles calling the ExternalCommands Console application and returning the results back from the execution.
    /// </summary>
    public class AppWrapper
    {
        const string ENTRY_POINT_ASSEMBLY = "Amazon.Lambda.TestTool.ExternalCommands.dll";
        
        private IList<string> _arguments;
        private string _command;

        public AppWrapper(string command, IList<string> arguments)
        {
            this._command = command;
            this._arguments = arguments;
        }

        public SimpleCommandResults Execute()
        {
            var info = CreateProcessStartInfo();
            var results = ExecuteSimpleCommandResults(info);
            return results;
        }

        private ProcessStartInfo CreateProcessStartInfo()
        {
            var args = new StringBuilder($"{ENTRY_POINT_ASSEMBLY} {this._command} ");
            if (this._arguments?.Count > 0)
            {
                foreach (var argument in _arguments)
                {
                    args.Append($"{argument} ");
                }
            }

            var workingDirectory = Path.Combine(Path.GetDirectoryName(typeof(AppWrapper).Assembly.Location), "ExternalCommands/App");

            if (!File.Exists(Path.Combine(workingDirectory, ENTRY_POINT_ASSEMBLY)))
            {
                throw new Exception(
                    $"Lambda Test Tool package malformed. Failed to find external commands assembly at {Path.Combine(workingDirectory, ENTRY_POINT_ASSEMBLY)}");
            }
            
            var dotnetCLI = Utils.FindExecutableInPath("dotnet.exe");
            var psi = new ProcessStartInfo
            {
                FileName = dotnetCLI,
                Arguments = args.ToString().Trim(),
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            return psi;
        }
        
        private SimpleCommandResults ExecuteSimpleCommandResults(ProcessStartInfo startInfo)
        {
            var standardOutBuffer = new StringBuilder();
            var standardErrorBuffer = new StringBuilder();
            
            var outhandler = (DataReceivedEventHandler)((o, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;
                standardOutBuffer.AppendLine(e.Data);
            });

            var errorhandler = (DataReceivedEventHandler)((o, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;
                standardErrorBuffer.AppendLine(e.Data);
            });

            using (var proc = new Process())
            {
                proc.StartInfo = startInfo;
                proc.Start();

                if(startInfo.RedirectStandardOutput)
                {
                    proc.ErrorDataReceived += errorhandler;
                    proc.OutputDataReceived += outhandler;
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    proc.EnableRaisingEvents = true;
                }

                proc.WaitForExit();

                return new SimpleCommandResults
                {
                    ExitCode = proc.ExitCode,
                    StandardOut = standardOutBuffer.ToString(),
                    StandardError = standardErrorBuffer.ToString()
                };
            }
        }

        public class SimpleCommandResults
        {
            public int ExitCode { get; set; }
            public string StandardOut { get; set; }
            public string StandardError { get; set; }
        }
    }
}