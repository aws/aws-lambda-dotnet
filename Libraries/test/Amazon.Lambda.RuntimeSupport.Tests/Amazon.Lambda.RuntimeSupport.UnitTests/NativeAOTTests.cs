using System;
using System.Diagnostics;
using System.Text;
using System.Reflection;

using Xunit;
using Xunit.Abstractions;
using System.IO;

#if NET8_0_OR_GREATER
namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class NativeAOTTests
    {
        private ITestOutputHelper _output;

        public NativeAOTTests(ITestOutputHelper output)
        {
            _output = output;
        }

#if DEBUG // Only running this test right now in local environment because CI system doesn't have Native AOT prereqs installed.
        [Fact]
#endif
        public void EnsureNoTrimWarningsDuringPublish()
        {
            var projectDirectory = FindProject("NativeAOTFunction");

            _output.WriteLine("dotnet publish " + projectDirectory);
            var output = ExecutePublish(projectDirectory);
            _output.WriteLine(output.Log);

            Assert.True(output.ExitCode == 0);
            Assert.DoesNotContain("AOT analysis warning", output.Log);
        }

        private (int ExitCode, string Log) ExecutePublish(string projectDirectory)
        {
            var buffer = new StringBuilder();
            var handler = (DataReceivedEventHandler)((o, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                buffer.AppendLine(e.Data);
            });

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish",
                WorkingDirectory = projectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            int exitCode;
            using (var proc = new Process())
            {
                proc.StartInfo = startInfo;
                proc.Start();

                if (startInfo.RedirectStandardOutput)
                {
                    proc.ErrorDataReceived += handler;
                    proc.OutputDataReceived += handler;
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    proc.EnableRaisingEvents = true;
                }

                proc.WaitForExit();

                
                exitCode = proc.ExitCode;
            }

            return (exitCode, buffer.ToString());
        }

        private string FindProject(string projectName)
        {
            var directory = Assembly.GetExecutingAssembly().Location;
            while (directory != null)
            {
                var fullpath = Path.Combine(directory, projectName);
                if (Directory.Exists(fullpath))
                {
                    return fullpath;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new Exception("Failed to find project directory " + projectName);
        }
    }
}
#endif