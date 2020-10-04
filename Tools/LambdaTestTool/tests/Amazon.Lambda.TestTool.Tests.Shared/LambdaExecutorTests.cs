using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.Runtime.LambdaMocks;
using Xunit;

namespace Amazon.Lambda.TestTool.Tests.Shared
{
    public class LambdaExecutorTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void NoPassedInContextShouldReturnOnlyLogger(string noContext)
        {
            var executor = new LambdaExecutor();
            var defaultLogger = new LocalLambdaLogger();

            var result = executor.GetLambdaContextForRequest(noContext, defaultLogger);

            Assert.Null(result.AwsRequestId);
            Assert.Null(result.FunctionName);
            Assert.Null(result.FunctionVersion);
            Assert.Null(result.LogGroupName);
            Assert.Null(result.ClientContext);
            Assert.Null(result.Identity);
            Assert.Null(result.InvokedFunctionArn);
            Assert.Null(result.LogStreamName);

            Assert.NotNull(result.Logger);

            Assert.Same(defaultLogger, result.Logger);
        }

        [Fact]
        public void PassedInAwsRequestIdShouldPopulateContextObject()
        {
            var executor = new LambdaExecutor();
            var defaultLogger = new LocalLambdaLogger();

            var awsRequstIdContext = "{\"AwsRequestId\":\"abc123\"}";
            
            var result = executor.GetLambdaContextForRequest(awsRequstIdContext, defaultLogger);

            Assert.NotNull(result.AwsRequestId);
            Assert.Equal("abc123", result.AwsRequestId);
            Assert.Null(result.FunctionName);
            Assert.Null(result.FunctionVersion);
            Assert.Null(result.LogGroupName);
            Assert.Null(result.ClientContext);
            Assert.Null(result.Identity);
            Assert.Null(result.InvokedFunctionArn);
            Assert.Null(result.LogStreamName);

            Assert.NotNull(result.Logger);

            Assert.Same(defaultLogger, result.Logger);
        }

        [Fact]
        public void PassedInFunctionNameShouldPopulateContextObject()
        {
            var executor = new LambdaExecutor();
            var defaultLogger = new LocalLambdaLogger();

            var awsRequstIdContext = "{\"FunctionName\":\"testname\"}";

            var result = executor.GetLambdaContextForRequest(awsRequstIdContext, defaultLogger);

            Assert.Null(result.AwsRequestId);
            Assert.NotNull(result.FunctionName);
            Assert.Equal("testname", result.FunctionName);
            Assert.Null(result.FunctionVersion);
            Assert.Null(result.LogGroupName);
            Assert.Null(result.ClientContext);
            Assert.Null(result.Identity);
            Assert.Null(result.InvokedFunctionArn);
            Assert.Null(result.LogStreamName);

            Assert.NotNull(result.Logger);

            Assert.Same(defaultLogger, result.Logger);
        }

        [Fact]
        public void PassedInFunctionVersionShouldPopulateContextObject()
        {
            var executor = new LambdaExecutor();
            var defaultLogger = new LocalLambdaLogger();

            var awsRequstIdContext = "{\"FunctionVersion\":\"99\"}";

            var result = executor.GetLambdaContextForRequest(awsRequstIdContext, defaultLogger);

            Assert.Null(result.AwsRequestId);
            Assert.Null(result.FunctionName);
            Assert.Equal("99", result.FunctionVersion);
            Assert.NotNull(result.FunctionVersion);
            Assert.Null(result.LogGroupName);
            Assert.Null(result.ClientContext);
            Assert.Null(result.Identity);
            Assert.Null(result.InvokedFunctionArn);
            Assert.Null(result.LogStreamName);

            Assert.NotNull(result.Logger);

            Assert.Same(defaultLogger, result.Logger);
        }

        [Fact]
        public void PassedInObjectShouldPopulateContextObject()
        {
            var executor = new LambdaExecutor();
            var defaultLogger = new LocalLambdaLogger();

            var awsRequstIdContext = "{\"AwsRequestId\":\"abc123\",\"FunctionName\":\"testname\",\"FunctionVersion\":\"99\",\"LogGroupName\":\"aLogGroup\",\"LogStreamName\":\"aLogStream\",\"InvokedFunctionArn\":\"someArn\"}";

            var result = executor.GetLambdaContextForRequest(awsRequstIdContext, defaultLogger);

            Assert.Equal("abc123", result.AwsRequestId);
            Assert.Equal("testname",result.FunctionName);
            Assert.Equal("99", result.FunctionVersion);
            Assert.Equal("aLogGroup", result.LogGroupName);
            Assert.Equal("someArn", result.InvokedFunctionArn);
            Assert.Equal("aLogStream", result.LogStreamName);

            Assert.Null(result.ClientContext);
            Assert.Null(result.Identity);
            Assert.NotNull(result.Logger);

            Assert.Same(defaultLogger, result.Logger);
        }

        [Fact]
        public void PassedInComplexIdentityObjectShouldPopulateContextObject()
        {
            var executor = new LambdaExecutor();
            var defaultLogger = new LocalLambdaLogger();

            var identityObject = "{\"Identity\": {\"IdentityId\":\"someId\", \"IdentityPoolId\":\"somePoolId\"}}";

            var result = executor.GetLambdaContextForRequest(identityObject, defaultLogger);

            Assert.Null(result.ClientContext);
            Assert.NotNull(result.Identity);
            Assert.NotNull(result.Logger);

            Assert.Equal("someId", result.Identity.IdentityId);
            Assert.Equal("somePoolId", result.Identity.IdentityPoolId);

            Assert.Same(defaultLogger, result.Logger);
        }

        [Fact]
        public void PassedInComplexClientContextObjectShouldPopulateContextObject()
        {
            var executor = new LambdaExecutor();
            var defaultLogger = new LocalLambdaLogger();

            var identityObject = "{\"ClientContext\": {\"Client\":{\"AppPackageName\":\"some App Name\"}, \"Environment\": {\"customEnv\":\"dictValue\"}, \"Custom\": {\"userSetProperty\":\"userSetValue\"}}}";

            var result = executor.GetLambdaContextForRequest(identityObject, defaultLogger);

            Assert.NotNull(result.ClientContext);
            Assert.Null(result.Identity);
            Assert.NotNull(result.Logger);

            Assert.Equal("some App Name", result.ClientContext.Client.AppPackageName);
            Assert.Equal("dictValue", result.ClientContext.Environment["customEnv"]);
            Assert.Equal("userSetValue", result.ClientContext.Custom["userSetProperty"]);

            Assert.Same(defaultLogger, result.Logger);
        }
    }
}
