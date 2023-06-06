using System;
using System.IO;
using Xunit;

using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.AspNetCoreServer.Internal;
using System.Collections.Generic;

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
        public void LambdaFunctionWithEnvironmentVariables()
        {
            var jsonFile = WriteTempConfigFile("{\"function-handler\" : \"Assembly::Type::Method\", \"environment-variables\" : \"key1=value1;key2=value2\"}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Single(configInfo.FunctionInfos);
                Assert.Equal("Assembly::Type::Method", configInfo.FunctionInfos[0].Handler);

                Assert.Equal(2, configInfo.FunctionInfos[0].EnvironmentVariables.Count);
                Assert.Equal("value1", configInfo.FunctionInfos[0].EnvironmentVariables["key1"]);
                Assert.Equal("value2", configInfo.FunctionInfos[0].EnvironmentVariables["key2"]);
            }
            finally
            {
                File.Delete(jsonFile);
            }
        }

        [Fact]
        public void LambdaFunctionWithQuotedEnvironmentVariables()
        {
            var jsonFile = WriteTempConfigFile("{\"function-handler\" : \"Assembly::Type::Method\", \"environment-variables\" : \"\\\"key1\\\"=\\\"value1\\\";\\\"key2\\\"=\\\"value2\\\"\"}");
            try
            {
                var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(jsonFile);
                Assert.Single(configInfo.FunctionInfos);
                Assert.Equal("Assembly::Type::Method", configInfo.FunctionInfos[0].Handler);

                Assert.Equal(2, configInfo.FunctionInfos[0].EnvironmentVariables.Count);
                Assert.Equal("value1", configInfo.FunctionInfos[0].EnvironmentVariables["key1"]);
                Assert.Equal("value2", configInfo.FunctionInfos[0].EnvironmentVariables["key2"]);
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
            
            Assert.Equal(4, configInfo.FunctionInfos.Count);
            Assert.Equal("default", configInfo.AWSProfile);
            Assert.Equal("us-west-2", configInfo.AWSRegion);
            
            Assert.Equal("AFunction", configInfo.FunctionInfos[0].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::AFunction", configInfo.FunctionInfos[0].Handler);
            
            Assert.Equal("MyHelloWorld", configInfo.FunctionInfos[1].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::HelloWorld", configInfo.FunctionInfos[1].Handler);

            Assert.Equal("MyHelloWorldImageCommand", configInfo.FunctionInfos[2].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::HelloWorldImageFunction", configInfo.FunctionInfos[2].Handler);

            Assert.Equal("MyToUpper", configInfo.FunctionInfos[3].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::ToUpper", configInfo.FunctionInfos[3].Handler);

        }
        
        [Fact]
        public void LoadServerlessResourceBasedYamlTemplateConfig()
        {
            var defaultsFilePath = TestUtils.GetLambdaFunctionSourceFile("ServerlessTemplateYamlExample", "aws-lambda-tools-defaults.json");

            var configInfo = LambdaDefaultsConfigFileParser.LoadFromFile(defaultsFilePath);
            
            Assert.Equal(4, configInfo.FunctionInfos.Count);
            Assert.Equal("default", configInfo.AWSProfile);
            Assert.Equal("us-west-2", configInfo.AWSRegion);
            
            Assert.Equal("AFunction", configInfo.FunctionInfos[0].Name);
            Assert.Equal("ServerlessTemplateYamlExample::ServerlessTemplateYamlExample.Functions::AFunction", configInfo.FunctionInfos[0].Handler);

            Assert.Equal("MyHelloWorld", configInfo.FunctionInfos[1].Name);
            Assert.Equal("ServerlessTemplateYamlExample::ServerlessTemplateYamlExample.Functions::HelloWorld", configInfo.FunctionInfos[1].Handler);

            Assert.Equal("MyHelloWorldImageCommand", configInfo.FunctionInfos[2].Name);
            Assert.Equal("ServerlessTemplateExample::ServerlessTemplateExample.Functions::HelloWorldImageFunction", configInfo.FunctionInfos[2].Handler);

            Assert.Equal("MyToUpper", configInfo.FunctionInfos[3].Name);
            Assert.Equal("ServerlessTemplateYamlExample::ServerlessTemplateYamlExample.Functions::ToUpper", configInfo.FunctionInfos[3].Handler);

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

        [Fact]
        public void ParseKeyValueParameter()
        {
            var parameters = new Dictionary<string, string>();
            LambdaDefaultsConfigFileParser.ParseKeyValueOption("Table=Blog", parameters);
            Assert.Single(parameters);
            Assert.Equal("Blog", parameters["Table"]);

            parameters = new Dictionary<string, string>();
            LambdaDefaultsConfigFileParser.ParseKeyValueOption("Table=Blog;", parameters);
            Assert.Single(parameters);
            Assert.Equal("Blog", parameters["Table"]);

            parameters = new Dictionary<string, string>();
            LambdaDefaultsConfigFileParser.ParseKeyValueOption("\"ConnectionString\"=\"User=foo;Password=test\"", parameters);
            Assert.Single(parameters);
            Assert.Equal("User=foo;Password=test", parameters["ConnectionString"]);
        }

        [Fact]
        public void ParseTwoKeyValueParameter()
        {
            var parameters = new Dictionary<string, string>();
            LambdaDefaultsConfigFileParser.ParseKeyValueOption("Table=Blog;Bucket=MyBucket", parameters);
            Assert.Equal(2, parameters.Count);

            Assert.Equal("Blog", parameters["Table"]);
            Assert.Equal("MyBucket", parameters["Bucket"]);

            parameters = new Dictionary<string, string>();
            LambdaDefaultsConfigFileParser.ParseKeyValueOption("\"ConnectionString1\"=\"User=foo;Password=test\";\"ConnectionString2\"=\"Password=test;User=foo\"", parameters);
            Assert.Equal(2, parameters.Count);
            Assert.Equal("User=foo;Password=test", parameters["ConnectionString1"]);
            Assert.Equal("Password=test;User=foo", parameters["ConnectionString2"]);
        }

        [Fact]
        public void ParseEmptyValue()
        {
            var parameters = new Dictionary<string, string>();
            LambdaDefaultsConfigFileParser.ParseKeyValueOption("ShouldCreateTable=true;BlogTableName=", parameters);
            Assert.Equal(2, parameters.Count);
            Assert.Equal("true", parameters["ShouldCreateTable"]);
            Assert.Equal("", parameters["BlogTableName"]);

            parameters = new Dictionary<string, string>();
            LambdaDefaultsConfigFileParser.ParseKeyValueOption("BlogTableName=;ShouldCreateTable=true", parameters);
            Assert.Equal(2, parameters.Count);
            Assert.Equal("true", parameters["ShouldCreateTable"]);
            Assert.Equal("", parameters["BlogTableName"]);
        }

        [Fact]
        public void ParseErrors()
        {
            var parameters = new Dictionary<string, string>();
            Assert.Throws<CommandLineParseException>(() => LambdaDefaultsConfigFileParser.ParseKeyValueOption("=aaa", parameters));
        }

        private string WriteTempConfigFile(string json)
        {
            var filePath = Path.GetTempFileName();
            File.WriteAllText(filePath, json);
            return filePath;
        }
    }
}