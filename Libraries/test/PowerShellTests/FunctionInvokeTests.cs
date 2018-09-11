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

namespace Amazon.Lambda.PowerShellTests
{
    public class FunctionInvokeTests
    {
        [Fact]
        public void FunctionWithBothInputAndContext()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var inputString = "\"YOU WILL BE LOWER\"";
            var function = new PowerShellScriptsAsFunctions.Function("FunctionTests.ps1")
            {
                PowerShellFunctionName = "ToLowerWithBothParams"
            };

            var resultStream = function.ExecuteFunction(ConvertToStream(inputString), context);
            var resultString = ConvertToString(resultStream);
            Assert.Contains("Calling ToLower with both parameters", logger.Buffer.ToString());
            Assert.Contains("TestLambdaContext", logger.Buffer.ToString());
            Assert.Equal(inputString.ToLower().Replace("\"", ""), resultString);
        }

        [Fact]
        public void FunctionWithNoContext()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var inputString = "\"YOU WILL BE LOWER\"";
            var function = new PowerShellScriptsAsFunctions.Function("FunctionTests.ps1")
            {
                PowerShellFunctionName = "ToLowerNoContext"
            };

            var resultStream = function.ExecuteFunction(ConvertToStream(inputString), context);
            var resultString = ConvertToString(resultStream);
            Assert.Contains("Calling ToLower with no context", logger.Buffer.ToString());
            Assert.DoesNotContain("TestLambdaContext", logger.Buffer.ToString());
            Assert.Equal(inputString.ToLower().Replace("\"", ""), resultString);
        }

        [Fact]
        public void FunctionWithNoParameters()
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            var function = new PowerShellScriptsAsFunctions.Function("FunctionTests.ps1")
            {
                PowerShellFunctionName = "NoParameters"
            };

            function.ExecuteFunction(new MemoryStream(), context);
            Assert.Contains("Calling NoParameters", logger.Buffer.ToString());
        }
    }
}
