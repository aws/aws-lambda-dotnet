using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.PowerShellHost;

using static Amazon.Lambda.PowerShellTests.TestUtilites;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.PowerShellTests
{
    public class ScriptInvokeTests
    {
        [Fact]
        public void ToUpperTest()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var inputString = "\"hello world from powershell\"";
            var function = new PowerShellScriptsAsFunctions.Function("ToUpperScript.ps1");

            var resultStream = function.ExecuteFunction(ConvertToStream(inputString), context);
            var resultString = ConvertToString(resultStream);
            Assert.Equal(inputString.ToUpper().Replace("\"", ""), resultString);
            Assert.Contains("Executing Script", logger.Buffer.ToString());
            Assert.Contains("Logging From Context", logger.Buffer.ToString());
        }

        [Fact]
        public void TestMarshalComplexResponse()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var inputString = "";
            var function = new PowerShellScriptsAsFunctions.Function("TestMarshalComplexResponse.ps1");

            var resultStream = function.ExecuteFunction(ConvertToStream(inputString), context);
            var resultString = ConvertToString(resultStream);

            var marshalledResponse = JsonConvert.DeserializeObject(resultString) as JObject;
            Assert.Equal("Hello World from PowerShell in Lambda", marshalledResponse["Body"].ToString());
            Assert.Equal(200, (int)marshalledResponse["StatusCode"]);
            Assert.Equal("text/plain", marshalledResponse["Headers"]["ContentType"]);
        }


        [Fact]
        public void CallingUnknownCommandTest()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var inputString = "";
            var function = new PowerShellScriptsAsFunctions.Function("CallingUnknownCommand.ps1");

            Exception foundException = null;
            try
            {
                function.ExecuteFunction(ConvertToStream(inputString), context);
            }
            catch(Exception e)
            {
                foundException = e;
            }

            Assert.NotNull(foundException);
            Assert.Contains("New-MagicBeanCmdLet", logger.Buffer.ToString());
            Assert.Contains("New-MagicBeanCmdLet", foundException.Message);
        }


        [Fact]
        public void TestExternalModuleLoaded()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var inputString = "";
            var function = new PowerShellScriptsAsFunctions.Function("TestExternalModuleLoaded.ps1");

            function.ExecuteFunction(ConvertToStream(inputString), context);
            Assert.Contains("Returns meta data about all the tasks defined in the provided psake script.", logger.Buffer.ToString());
        }


        [Fact]
        public void UseAWSPowerShellCmdLetTest()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var inputString = "";
            var function = new PowerShellScriptsAsFunctions.Function("UseAWSPowerShellCmdLetTest.ps1");

            var resultStream = function.ExecuteFunction(ConvertToStream(inputString), context);
            var resultString = ConvertToString(resultStream);
            Assert.Contains(@"Importing module ./Modules/AWSPowerShell.NetCore", logger.Buffer.ToString().Replace('\\', '/'));
            Assert.Contains("ServiceName", resultString);
            Assert.Contains("AWS Lambda", resultString);
        }

#if NETCOREAPP3_1
        [Fact]
        public void ForObjectParallelTest()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var inputString = "";
            var function = new PowerShellScriptsAsFunctions.Function("ForObjectParallel.ps1");

            function.ExecuteFunction(ConvertToStream(inputString), context);

            Assert.Contains("Running against: 1 for SharedVariable: Hello Shared Variable", logger.Buffer.ToString());
            Assert.Contains("Running against: 50 for SharedVariable: Hello Shared Variable", logger.Buffer.ToString());
            Assert.Contains("Running against: 100 for SharedVariable: Hello Shared Variable", logger.Buffer.ToString());
        }
#endif

        [Fact]
        public void CheckTempEnvironmentVariable()
        {
            // Non Lambda Environment
            {
                var logger = new TestLambdaLogger();
                var context = new TestLambdaContext
                {
                    Logger = logger
                };

                var inputString = "\"hello world from powershell\"";
                var function = new PowerShellScriptsAsFunctions.Function("TempEnvCheck.ps1");

                var resultStream = function.ExecuteFunction(ConvertToStream(inputString), context);
                var resultString = ConvertToString(resultStream);
                Assert.Equal(Path.GetTempPath(), resultString);
            }

            var currentHome = Environment.GetEnvironmentVariable("HOME");
            // Lambda environment
            try
            {
                Environment.SetEnvironmentVariable("HOME", null);
                Environment.SetEnvironmentVariable("LAMBDA_TASK_ROOT", "/var/task");

                var logger = new TestLambdaLogger();
                var context = new TestLambdaContext
                {
                    Logger = logger
                };

                var inputString = "\"hello world from powershell\"";
                var function = new PowerShellScriptsAsFunctions.Function("TempEnvCheck.ps1");

                var resultStream = function.ExecuteFunction(ConvertToStream(inputString), context);
                var resultString = ConvertToString(resultStream);
                Assert.Equal("/tmp", resultString);

                Assert.Contains("/tmp/home", logger.Buffer.ToString());
            }
            finally
            {
                Environment.SetEnvironmentVariable("LAMBDA_TASK_ROOT", null);
                Environment.SetEnvironmentVariable("HOME", currentHome);
            }
        }
    }
}
