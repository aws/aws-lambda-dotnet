using System;
using System.IO;
using Xunit;

using Amazon.Lambda.TestTool.Runtime;

namespace Amazon.Lambda.TestTool.Tests
{
    public class LoadLambdasDllFileTests
    {
        
        [Fact]
        public void InitializeLambdaRuntimeWithSpecificDllFilename()
        {
            var configFile = TestUtils.GetLambdaFunctionSourceFile("ToUpperFunc", "aws-lambda-tools-defaults.json");
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ToUpperFunc");

            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(configFile);
            Assert.Single(configInfo.FunctionInfos);
            
            var runtime = LocalLambdaRuntime.Initialize(buildPath, "ToUpperFunc.dll");

            var functions = runtime.LoadLambdaFunctions(configInfo.FunctionInfos);
            
            Assert.Equal(1, functions.Count);

            var function = functions[0];
            Assert.True(function.IsSuccess);
            Assert.NotNull(function.LambdaAssembly);
            Assert.NotNull(function.LambdaType);
            Assert.NotNull(function.LambdaMethod);
            
            Assert.NotNull(function.Serializer);
        }

        [Fact]
        public void InitializeLambdaRuntimeWithIncorrectDllFilename()
        {
            var configFile = TestUtils.GetLambdaFunctionSourceFile("ToUpperFunc", "aws-lambda-tools-defaults.json");
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ToUpperFunc");

            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(configFile);
            Assert.Single(configInfo.FunctionInfos);

            Assert.ThrowsAny<Exception>(() =>
                LocalLambdaRuntime.Initialize(buildPath, "incorrect-assembly.dll")
            );
        }
    }
}