using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

using Amazon.Lambda.TestTool.Runtime;

namespace Amazon.Lambda.TestTool.Tests
{
    public class InvokeFunctionTests
    {

        [Fact]
        public void StringToStringWithContextTest()
        {
            var payload = "\"TestData\"";

            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::StringToStringWithContext",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"StringToStringWithContext-TestData\"", response.Response);
        }
        
        
        [Fact]
        public void NoParametersTest()
        {
            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::NoParameters",
                null);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"NoParameters\"", response.Response);
        }
        
        [Fact]
        public void VoidReturnTest()
        {
            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::VoidReturn",
                null);

            Assert.True(response.IsSuccess);
            Assert.Null(response.Response);
        }

        [Fact]
        public void NoContextWithParameterTest()
        {
            var payload = "\"TestData\"";

            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::NoContextWithParameter",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"NoContextWithParameter-TestData\"", response.Response);
        }
        
        [Fact]
        public void TheStaticMethodTest()
        {
            var payload = "\"TestData\"";

            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.StaticMethods::TheStaticMethod",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"TheStaticMethod-TestData\"", response.Response);
        }

        [Fact]
        public void WithGenericParameterTest()
        {
            var payload = "[\"Value1\", \"Value2\"]";

            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.InstanceMethods::WithGenericParameter",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"Value1,Value2\"", response.Response);
        }

        [Fact]
        public void TaskWithNoResultTest()
        {
            var payload = "\"TestData\"";

            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.AsyncMethods::TaskWithNoResult",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Null(response.Response);
        } 
        
        [Fact]
        public void TaskWithResultTest()
        {
            var payload = "\"TestData\"";

            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.AsyncMethods::TaskWithResult",
                payload);

            Assert.True(response.IsSuccess);
            Assert.Equal("\"TaskWithResult-TestData\"", response.Response);
        }

        [Fact]
        public void AsyncNoResultThrowExceptionTest()
        {
            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.ErrorFunctions::AsyncNoResultThrowException",
                null);

            Assert.False(response.IsSuccess);
            Assert.NotNull(response.Error);
        }

        [Fact]
        public void AsyncWithResultThrowExceptionTest()
        {
            var response = ExecuteFunction(
                "FunctionSignatureExamples::FunctionSignatureExamples.ErrorFunctions::AsyncWithResultThrowException",
                null);

            Assert.False(response.IsSuccess);
            Assert.NotNull(response.Error);
        }

        private ExecutionResponse ExecuteFunction(string handler, string payload)
        {
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("FunctionSignatureExamples");

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
            
            var response = runtime.ExecuteLambdaFunction(request);
            return response;
        }
    }
}