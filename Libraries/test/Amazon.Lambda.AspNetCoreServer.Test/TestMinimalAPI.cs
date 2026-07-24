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

                // Invoke "dotnet" directly rather than through a shell. Routing "dotnet run ..." through a
                // shell as a single Arguments string is not portable: on Unix .NET splits the string into an
                // argv array, so "sh -c dotnet run <req> <resp>" makes the shell run just "dotnet" (with the
                // rest as positional parameters $0/$1/...), which prints the dotnet usage text and exits 129.
                // Passing each argument via ArgumentList lets the runtime quote them correctly on every OS.
                // "dotnet run" builds the app first, which by default spins up persistent build-server
                // processes (the MSBuild node and the Roslyn "VBCSCompiler" shared-compilation server).
                // Those servers inherit the redirected stdout/stderr pipe handles below and linger for
                // ~15 minutes after "dotnet run" exits. The test runner waits for those inherited pipe
                // handles to close before the test host process can exit, so a leftover build server makes
                // the whole "dotnet test" invocation hang (observed as a multi-hour stall in CI until it is
                // killed). Disable both persistent servers so nothing outlives "dotnet run" holding the
                // pipes open. UseSharedCompilation is an MSBuild property, so it must be passed as a build
                // property (not an environment variable) for it to take effect; the app arguments are
                // separated with "--" so they are not parsed as "dotnet run" options.
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = "dotnet";
                processStartInfo.ArgumentList.Add("run");
                processStartInfo.ArgumentList.Add("--property:UseSharedCompilation=false");
                processStartInfo.ArgumentList.Add("--");
                processStartInfo.ArgumentList.Add(requestFilePath);
                processStartInfo.ArgumentList.Add(responseFilePath);
                processStartInfo.WorkingDirectory = GetTestAppDirectory();

                // Capture stdout/stderr from the "dotnet run" child process so that, when it exits non-zero, the
                // underlying build/runtime output is surfaced in the test failure instead of just an exit code.
                processStartInfo.UseShellExecute = false;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;

                // Also disable the persistent MSBuild node server (env-controlled) so it does not inherit
                // the redirected pipe handles and linger; see the build-server note above.
                processStartInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";


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
                        try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
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
        }
    }
}
