using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.APIGatewayEvents;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class TestMinimalAPI : IClassFixture<TestMinimalAPI.TestMinimalAPIAppFixture>
    {
        readonly TestMinimalAPIAppFixture _fixture;

        public TestMinimalAPI(TestMinimalAPI.TestMinimalAPIAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestMapPostComplexType()
        {
            var response = _fixture.ExecuteRequest<APIGatewayProxyResponse>("minimal-api-post.json");
            Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("works:string", response.Body);
        }

        public class TestMinimalAPIAppFixture : IDisposable
        {
            readonly object lock_process = new object();
            public TestMinimalAPIAppFixture()
            {
            }

            public void Dispose()
            {
            }


            public T ExecuteRequest<T>(string eventFilePath)
            {
                var requestFilePath = Path.Combine(Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location), eventFilePath);
                var responseFilePath = Path.GetTempFileName();

                var comamndArgument = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c" : $"-c";
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = GetSystemShell();
                processStartInfo.Arguments = $"{comamndArgument} dotnet run \"{requestFilePath}\" \"{responseFilePath}\"";
                processStartInfo.WorkingDirectory = GetTestAppDirectory();

                // Capture stdout/stderr from the "dotnet run" shell out so that, when it exits non-zero, the
                // underlying build/runtime output is surfaced in the test failure instead of just an exit code.
                processStartInfo.UseShellExecute = false;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;


                lock (lock_process)
                {
                    using var process = Process.Start(processStartInfo);

                    // Read both streams asynchronously; reading them synchronously can deadlock if one pipe's
                    // buffer fills while we're blocked waiting on the other.
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();
                    process.OutputDataReceived += (sender, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    process.ErrorDataReceived += (sender, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(45000))
                    {
                        try { process.Kill(); } catch { /* best effort */ }
                        throw new Exception(
                            "Process timed out after 45000ms." + BuildProcessOutput(stdout, stderr));
                    }

                    // Ensure the asynchronous output handlers have flushed all buffered data before we read it.
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception(
                            "Process failed with exit code: " + process.ExitCode + BuildProcessOutput(stdout, stderr));
                    }

                    if(!File.Exists(responseFilePath))
                    {
                        throw new Exception("No response file found");
                    }

                    using var responseFileStream = File.OpenRead(responseFilePath);

                    var serializer = new DefaultLambdaJsonSerializer();
                    var response = serializer.Deserialize<T>(responseFileStream);

                    return response;
                }
            }

            private static string BuildProcessOutput(StringBuilder stdout, StringBuilder stderr)
            {
                return $"{Environment.NewLine}--- STDOUT ---{Environment.NewLine}{stdout}" +
                       $"{Environment.NewLine}--- STDERR ---{Environment.NewLine}{stderr}";
            }

            private string GetTestAppDirectory()
            {
                var path = GetType().GetTypeInfo().Assembly.Location;
                while(!string.Equals(new DirectoryInfo(path).Name, "test"))
                {
                    path = Directory.GetParent(path).FullName;
                }

                return Path.GetFullPath(Path.Combine(path, "TestMinimalAPIApp"));
            }

            private string GetSystemShell()
            {
                if (TryGetEnvironmentVariable("COMSPEC", out var comspec))
                {
                    return comspec!;
                }

                if (TryGetEnvironmentVariable("SHELL", out var shell))
                {
                    return shell!;
                }

                // fallback to defaults
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh";
            }

            private bool TryGetEnvironmentVariable(string variable, out string value)
            {
                value = Environment.GetEnvironmentVariable(variable);
                return !string.IsNullOrEmpty(value);
            }
        }
    }
}
