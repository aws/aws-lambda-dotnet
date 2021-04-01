using System;
using System.IO;
using Xunit;

using Amazon.Lambda.TestTool.Runtime;

namespace Amazon.Lambda.TestTool.Tests
{
    public class DefaultsFileParseTests
    {
        [Fact]
        public void LambdaFunctionWithNoName()
        {
            var jsonFile = WriteTempConfigFile("{\"function-handler\" : \"Assembly::Type::Method\"}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Single(configInfo.FunctionInfos);
                Assert.Equal("Assembly::Type::Method", configInfo.FunctionInfos[0].Handler);
                Assert.Equal("Assembly::Type::Method", configInfo.FunctionInfos[0].Name);
            }
            finally
            {
                File.Delete(jsonFile);
            }
        }
        
        [Fact]
        public void LambdaFunctionWithName()
        {
            var jsonFile = WriteTempConfigFile("{\"function-handler\" : \"Assembly::Type::Method\", \"function-name\" : \"TheFunc\"}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Single(configInfo.FunctionInfos);
                Assert.Equal("Assembly::Type::Method", configInfo.FunctionInfos[0].Handler);
                Assert.Equal("TheFunc", configInfo.FunctionInfos[0].Name);
            }
            finally
            {
                File.Delete(jsonFile);
            }
        }
        [Fact]
        public void LambdaFunctionWithImageCommand()
        {
            var jsonFile = WriteTempConfigFile("{\"image-command\" : \"Assembly::Type::Method\", \"function-name\" : \"TheFunc\"}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Single(configInfo.FunctionInfos);
                Assert.Equal("Assembly::Type::Method", configInfo.FunctionInfos[0].Handler);
                Assert.Equal("TheFunc", configInfo.FunctionInfos[0].Name);
            }
            finally
            {
                File.Delete(jsonFile);
            }
        }

        [Fact]
        public void NoProfile()
        {
            var jsonFile = WriteTempConfigFile("{}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Equal("default", configInfo.AWSProfile);            
            }
            finally
            {
                File.Delete(jsonFile);
            }
        }
        
        [Fact]
        public void NonDefaultProfile()
        {
            var jsonFile = WriteTempConfigFile("{\"profile\" : \"test-profile\"}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Equal("test-profile", configInfo.AWSProfile);            
            }
            finally
            {
                File.Delete(jsonFile);
            }
        }
        
        [Fact]
        public void NoRegion()
        {
            var jsonFile = WriteTempConfigFile("{}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Null(configInfo.AWSRegion);            
            }
            finally
            {
                File.Delete(jsonFile);
            }
        }
        
        [Fact]
        public void SetRegion()
        {
            var jsonFile = WriteTempConfigFile("{\"region\" : \"us-west-2\"}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Equal("us-west-2", configInfo.AWSRegion);            
            }
            finally
            {
                File.Delete(jsonFile);
            }
        }

        [Fact]
        public void LoadServerlessTemplateConfig()
        {
            var defaultsFilePath = TestUtils.GetLambdaFunctionSourceFile("ServerlessTemplateExample", "aws-lambda-tools-defaults.json");

            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(defaultsFilePath);
            
            Assert.Equal(3, configInfo.FunctionInfos.Count);
            Assert.Equal("default", configInfo.AWSProfile);
            Assert.Equal("us-west-2", configInfo.AWSRegion);
            
            Assert.Equal("MyHelloWorld", configInfo.FunctionInfos[0].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::HelloWorld", configInfo.FunctionInfos[0].Handler);

            Assert.Equal("MyHelloWorldImageCommand", configInfo.FunctionInfos[1].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::HelloWorldImageFunction", configInfo.FunctionInfos[1].Handler);

            Assert.Equal("MyToUpper", configInfo.FunctionInfos[2].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::ToUpper", configInfo.FunctionInfos[2].Handler);

        }
        
        [Fact]
        public void LoadServerlessResourceBasedYamlTemplateConfig()
        {
            var defaultsFilePath = TestUtils.GetLambdaFunctionSourceFile("ServerlessTemplateYamlExample", "aws-lambda-tools-defaults.json");

            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(defaultsFilePath);
            
            Assert.Equal(3, configInfo.FunctionInfos.Count);
            Assert.Equal("default", configInfo.AWSProfile);
            Assert.Equal("us-west-2", configInfo.AWSRegion);
            
            Assert.Equal("MyHelloWorld", configInfo.FunctionInfos[0].Name);
            Assert.Equal("ServerlessTemplateYamlExample::ServerlessTemplateYamlExample.Functions::HelloWorld", configInfo.FunctionInfos[0].Handler);

            Assert.Equal("MyHelloWorldImageCommand", configInfo.FunctionInfos[1].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::HelloWorldImageFunction", configInfo.FunctionInfos[1].Handler);

            Assert.Equal("MyToUpper", configInfo.FunctionInfos[2].Name);
            Assert.Equal("ServerlessTemplateYamlExample::ServerlessTemplateYamlExample.Functions::ToUpper", configInfo.FunctionInfos[2].Handler);

        }        

        [Fact]
        public void LoadServerlessFunctionBasedYamlTemplateConfig()
        {
            var defaultsFilePath = TestUtils.GetLambdaFunctionSourceFile("ServerlessFunctionTemplateYamlExample", "aws-lambda-tools-defaults.json");

            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(defaultsFilePath);
            
            Assert.Equal(3, configInfo.FunctionInfos.Count);
            Assert.Equal("default", configInfo.AWSProfile);
            Assert.Equal("us-west-2", configInfo.AWSRegion);
            
            Assert.Equal("create", configInfo.FunctionInfos[0].Name);
            Assert.Equal("DotNetServerless.Lambda::DotNetServerless.Lambda.Functions.CreateItemFunction::Run", configInfo.FunctionInfos[0].Handler);
            
            Assert.Equal("get", configInfo.FunctionInfos[1].Name);
            Assert.Equal("DotNetServerless.Lambda::DotNetServerless.Lambda.Functions.GetItemFunction::Run", configInfo.FunctionInfos[1].Handler);
            
                  
            Assert.Equal("update", configInfo.FunctionInfos[2].Name);
            Assert.Equal("DotNetServerless.Lambda::DotNetServerless.Lambda.Functions.UpdateItemFunction::Run", configInfo.FunctionInfos[2].Handler);

        }        

        private string WriteTempConfigFile(string json)
        {
            var filePath = Path.GetTempFileName();
            File.WriteAllText(filePath, json);
            return filePath;
        }
    }
}