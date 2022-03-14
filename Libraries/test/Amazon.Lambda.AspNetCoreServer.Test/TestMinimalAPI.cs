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
        TestMinimalAPIAppFixture _fixture;

        public TestMinimalAPI(TestMinimalAPI.TestMinimalAPIAppFixture fixture)
        {
            this._fixture = fixture;
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
            object lock_process = new object();
            public TestMinimalAPIAppFixture()
            {
            }

            public void Dispose()
            {
            }


            public T ExecuteRequest<T>(string eventFilePath)
            {
                var requestFilePath = Path.Combine(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location), eventFilePath);
                var responseFilePath = Path.GetTempFileName();

                var comamndArgument = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c" : $"-c";
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = GetSystemShell();
                processStartInfo.Arguments = $"{comamndArgument} dotnet run \"{requestFilePath}\" \"{responseFilePath}\"";
                processStartInfo.WorkingDirectory = GetTestAppDirectory();


                lock (lock_process)
                {
                    using var process = Process.Start(processStartInfo);
                    process.WaitForExit(15000);

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

            private string GetTestAppDirectory()
            {
                var path = this.GetType().GetTypeInfo().Assembly.Location;
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

            private bool TryGetEnvironmentVariable(string variable, out string? value)
            {
                value = Environment.GetEnvironmentVariable(variable);
                return !string.IsNullOrEmpty(value);
            }
        }
    }
}
