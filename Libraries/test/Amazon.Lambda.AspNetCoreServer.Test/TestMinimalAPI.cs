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

                // Execute the already-built TestMinimalAPIApp assembly directly with "dotnet <app>.dll"
                // rather than "dotnet run". "dotnet run" builds the app at test time, which caused a series
                // of CI problems: it spins up persistent build-server processes (reusable MSBuild worker
                // nodes and the Roslyn shared-compilation server) that inherit the redirected stdout/stderr
                // pipes and linger, hanging "dotnet test" for hours; and its build step races the launched
                // app over the app's runtimeconfig.json ("The process cannot access the file ...
                // runtimeconfig.json because it is being used by another process"). The app is a member of
                // Libraries.sln, so it is already compiled by the solution build before the tests run;
                // invoking the built DLL needs no build step and avoids all of the above.
                var appDll = GetTestAppAssemblyPath();
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = "dotnet";
                processStartInfo.ArgumentList.Add(appDll);
                processStartInfo.ArgumentList.Add(requestFilePath);
                processStartInfo.ArgumentList.Add(responseFilePath);
                processStartInfo.WorkingDirectory = Path.GetDirectoryName(appDll);

                // Capture stdout/stderr from the child process so that, when it exits non-zero, the
                // underlying runtime output is surfaced in the test failure instead of just an exit code.
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

            // Locates the compiled TestMinimalAPIApp assembly. TestMinimalAPIApp targets net10.0 only and is
            // built by the solution build into bin/{Configuration}/net10.0. The configuration is taken from
            // this test assembly's own path (…/bin/{Configuration}/{tfm}/…) so the Debug or Release build is
            // matched regardless of how the tests were launched.
            private string GetTestAppAssemblyPath()
            {
                var testDir = FindAncestorDirectory("test");
                var configuration =
#if DEBUG
                    "Debug";
#else
                    "Release";
#endif
                var appDll = Path.GetFullPath(Path.Combine(
                    testDir, "TestMinimalAPIApp", "bin", configuration, "net10.0", "TestMinimalAPIApp.dll"));

                if (!File.Exists(appDll))
                {
                    throw new FileNotFoundException(
                        $"TestMinimalAPIApp was not found at '{appDll}'. It should have been compiled by the " +
                        "solution build before the tests run.", appDll);
                }

                return appDll;
            }

            private string FindAncestorDirectory(string directoryName)
            {
                var path = GetType().GetTypeInfo().Assembly.Location;
                while (!string.Equals(new DirectoryInfo(path).Name, directoryName))
                {
                    path = Directory.GetParent(path).FullName;
                }

                return path;
            }
        }
    }
}
