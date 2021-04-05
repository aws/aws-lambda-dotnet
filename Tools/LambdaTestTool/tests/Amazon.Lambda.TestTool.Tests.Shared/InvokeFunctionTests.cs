using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

using Amazon.Lambda.TestTool.Runtime;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool.Tests
{
    public class InvokeFunctionTests
    {

        [Fact]
        public async Task StringToStringWithContextTest()
        {
            var payload = "\"TestData\"";

            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::StringToStringWithContext",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"StringToStringWithContext-TestData\"", response.Response);
        }
        
        
        [Fact]
        public async Task NoParametersTest()
        {
            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::NoParameters",
                null);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"NoParameters\"", response.Response);
        }
        
        [Fact]
        public async Task VoidReturnTest()
        {
            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::VoidReturn",
                null);

            Assert.True(response.IsSuccess);
            Assert.Null(response.Response);
        }

        [Fact]
        public async Task NoContextWithParameterTest()
        {
            var payload = "\"TestData\"";

            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::NoContextWithParameter",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"NoContextWithParameter-TestData\"", response.Response);
        }
        
        [Fact]
        public async Task TheStaticMethodTest()
        {
            var payload = "\"TestData\"";

            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.StaticMethods::TheStaticMethod",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"TheStaticMethod-TestData\"", response.Response);
        }

        [Fact]
        public async Task WithGenericParameterTest()
        {
            var payload = "[\"Value1\", \"Value2\"]";

            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::WithGenericParameter",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"Value1,Value2\"", response.Response);
        }

        [Fact]
        public async Task WithEventParameterTest()
        {

            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::WithEventParameter",
                null);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"WithEventParameter-event-not-null\"", response.Response);
        }

        [Fact]
        public async Task TaskWithNoResultTest()
        {
            var payload = "\"TestData\"";

            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.AsyncMethods::TaskWithNoResult",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Null(response.Response);
        } 
        
        [Fact]
        public async Task TaskWithResultTest()
        {
            var payload = "\"TestData\"";

            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.AsyncMethods::TaskWithResult",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"TaskWithResult-TestData\"", response.Response);
        }

        [Fact]
        public async Task AsyncNoResultThrowExceptionTest()
        {
            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.ErrorFunctions::AsyncNoResultThrowException",
                null);

            Assert.False(response.IsSuccess);
            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task AsyncWithResultThrowExceptionTest()
        {
            var response = await ExecuteFunctionAsync(
                "FunctionSignatureExamples::FunctionSignatureExamples.ErrorFunctions::AsyncWithResultThrowException",
                null);

            Assert.False(response.IsSuccess);
            Assert.NotNull(response.Error);
        }

#if NETCORE_3_1
        [Fact]
        public async Task CallValuesControllerInAspNetCoreApp()
        {
            var payload = File.ReadAllText("call-valuescontroller-request.txt");
            var response = await ExecuteFunctionAsync("AspNetCoreAPIExample",
                "AspNetCoreAPIExample::AspNetCoreAPIExample.LambdaEntryPoint::FunctionHandlerAsync",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Contains("value1", response.Response);
        }
#endif

        private async Task<ExecutionResponse> ExecuteFunctionAsync(string handler, string payload)
        {
            return await ExecuteFunctionAsync("FunctionSignatureExamples", handler, payload);
        }

        private async Task<ExecutionResponse> ExecuteFunctionAsync(string projectName, string handler, string payload)
        {
            var buildPath = TestUtils.GetLambdaFunctionBuildPath(projectName);

            var runtime = LocalLambdaRuntime.Initialize(buildPath);
            var configInfo = new LambdaFunctionInfo
            {
                Name = "TestMethod",
                Handler = handler
            };

            var function = runtime.LoadLambdaFunction(configInfo);
            Assert.True(function.IsSuccess);

            var request = new ExecutionRequest()
            {
                Function = function,
                AWSRegion = "us-west-2",
                Payload = payload
            };

            var response = await runtime.ExecuteLambdaFunctionAsync(request);
            return response;
        }
    }
}