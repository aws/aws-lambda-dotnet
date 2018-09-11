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
    public class ExceptionHandlingTests
    {

        [Fact]
        public void CheckIfErrorCode()
        {
            Assert.True(ExceptionManager.IsErrorCode("ErrorCode1"));
            Assert.True(ExceptionManager.IsErrorCode("Error_Code"));
            Assert.True(ExceptionManager.IsErrorCode("ErrorϴCode"));

            Assert.False(ExceptionManager.IsErrorCode("1ErrorCode"));
            Assert.False(ExceptionManager.IsErrorCode("Error Code"));
            Assert.False(ExceptionManager.IsErrorCode("Error@Code"));
        }

        [Fact]
        public void ValidateSystemException()
        {
            ExceptionValidator("ThrowSystemException", "FileNotFoundException", null);
        }

        [Fact]
        public void ValidateCustomException()
        {
            ExceptionValidator("CustomException", "AccountNotFound", "The Account is not found");
        }

        [Fact]
        public void CustomExceptionNoMessage()
        {
            ExceptionValidator("CustomExceptionNoMessage", "CustomExceptionNoMessage", "CustomExceptionNoMessage");
        }

        [Fact]
        public void TestMultipleInvokesWithSameCustomException()
        {
            PowerShellScriptsAsFunctions.Function function = new PowerShellScriptsAsFunctions.Function("ErrorExamples.ps1");

            ExceptionValidator(function, "CustomException", "AccountNotFound", "The Account is not found");
            ExceptionValidator(function, "CustomException", "AccountNotFound", "The Account is not found");
        }

        [Fact]
        public void TestMultipleInvokesWithDifferentCustomException()
        {
            PowerShellScriptsAsFunctions.Function function = new PowerShellScriptsAsFunctions.Function("ErrorExamples.ps1");

            ExceptionValidator(function, "CustomException", "AccountNotFound", "The Account is not found");
            ExceptionValidator(function, "CustomExceptionNoMessage", "CustomExceptionNoMessage", null);
        }

        [Fact]
        public void ThrowWithStringMessage()
        {
            ExceptionValidator("ThrowWithStringMessage", "RuntimeException", "Here is your error");
        }

        [Fact]
        public void ThrowWithStringErrorCode()
        {
            ExceptionValidator("ThrowWithStringErrorCode", "ErrorCode42", "ErrorCode42");
        }

        [Fact]
        public void WriteErrorWithMessageTest()
        {
            ExceptionValidator("WriteErrorWithMessageTest", "WriteErrorException", "Testing out Write-Error");
        }

        [Fact]
        public void WriteErrorWithExceptionTest()
        {
            ExceptionValidator("WriteErrorWithExceptionTest", "FileNotFoundException", null);
        }

        private void ExceptionValidator(string psFunction, string exceptionType, string message)
        {
            PowerShellScriptsAsFunctions.Function function = new PowerShellScriptsAsFunctions.Function("ErrorExamples.ps1");
            ExceptionValidator(function, psFunction, exceptionType, message);
        }

        private void ExceptionValidator(PowerShellScriptsAsFunctions.Function function, string psFunction, string exceptionType, string message)
        {
            var logger = new TestLambdaLogger();
            var context = new TestLambdaContext
            {
                Logger = logger
            };

            function.PowerShellFunctionName = psFunction;

            Exception foundException = null;
            try
            {
                function.ExecuteFunction(new MemoryStream(), context);
            }
            catch (Exception e)
            {
                foundException = e;
            }

            Assert.NotNull(foundException);
            Assert.True(foundException.GetType().Name.EndsWith(exceptionType));

            if(message != null)
            {
                Assert.Equal(message, foundException.Message);
            }

        }
    }
}
