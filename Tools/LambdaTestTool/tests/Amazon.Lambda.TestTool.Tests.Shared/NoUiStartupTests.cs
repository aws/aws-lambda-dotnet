using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

using Amazon.Lambda.TestTool;

namespace Amazon.Lambda.TestTool.Tests
{
    public class NoUiStartupTests
    {
        

        [Fact]
        public void DirectFunctionCallFromConfig()
        {
            var runConfiguration = CreateRunConfiguration();
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ToUpperFunc");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "\"hello WORLD\"" }, runConfiguration);
            Assert.Contains("HELLO WORLD", runConfiguration.OutputWriter.ToString());
        }

        [Fact]
        public void DirectFunctionCallNotFromConfig()
        {
            var runConfiguration = CreateRunConfiguration();
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ToUpperFunc");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "\"hello WORLD\"", "--function-handler", "ToUpperFunc::ToUpperFunc.Function::ToLower" }, runConfiguration);
            Assert.Contains("hello world", runConfiguration.OutputWriter.ToString());
        }

        [Fact]
        public void PayloadFromFile()
        {
            var runConfiguration = CreateRunConfiguration();
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ToUpperFunc");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "payload-sample.json" }, runConfiguration);
            Assert.Contains("HELLO FROM PAYLOAD FILE", runConfiguration.OutputWriter.ToString());
        }

        [Fact]
        public void ServerlessTemplateFunctionCall()
        {
            var runConfiguration = CreateRunConfiguration();
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ServerlessTemplateExample");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "\"hello WORLD\"", "--function-handler", "ServerlessTemplateExample::ServerlessTemplateExample.Functions::HelloWorld" }, runConfiguration);
            Assert.Contains("Hello World Test", runConfiguration.OutputWriter.ToString());
        }

        [Fact]
        public void ProjectHasMultipleLambdaFunctionsAndFunctionHandlerNotGiven()
        {
            var runConfiguration = CreateRunConfiguration();
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ServerlessTemplateExample");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "\"hello WORLD\"" }, runConfiguration);
            Assert.Contains("Project has more then one Lambda function defined. Use the --function-handler switch to identify the Lambda code to execute.", runConfiguration.OutputWriter.ToString());
        }

        [Fact]
        public void UseProfileAndRegionFromConfigFile()
        {
            var runConfiguration = CreateRunConfiguration();
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ToUpperFunc");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "\"hello WORLD\"" }, runConfiguration);
            Assert.Contains("HELLO WORLD", runConfiguration.OutputWriter.ToString());
            Assert.Contains("Setting AWS_REGION environment variable to us-west-2.", runConfiguration.OutputWriter.ToString());
        }

        [Fact]
        public void UseExplicitRegion()
        {
            var runConfiguration = CreateRunConfiguration();
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("ToUpperFunc");

            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--payload", "\"hello WORLD\"", "--region", "fake-region" }, runConfiguration);
            Assert.Contains("HELLO WORLD", runConfiguration.OutputWriter.ToString());
            Assert.Contains("Setting AWS_REGION environment variable to fake-region.", runConfiguration.OutputWriter.ToString());
        }

        [Fact]
        public void NoProfileAndRegion()
        {
            var runConfiguration = CreateRunConfiguration();
            var buildPath = TestUtils.GetLambdaFunctionBuildPath("FunctionSignatureExamples");

            // Profile is set to a nonexisting profile because if no profile is set on the command line or config file then the default profile will be set if it exists.
            TestToolStartup.Startup("Unit Tests", null, new string[] { "--path", buildPath, "--no-ui", "--profile", "do-no-exist", "--payload", "\"hello WORLD\"", "--function-handler", "FunctionSignatureExamples::FunctionSignatureExamples.StaticMethods::TheStaticMethod" }, runConfiguration);            
            Assert.Contains("Warning: Profile do-no-exist not found in the aws credential store.", runConfiguration.OutputWriter.ToString());
            Assert.Contains("No default AWS region configured. The --region switch can be used to configure an AWS Region.", runConfiguration.OutputWriter.ToString());
        }

        private TestToolStartup.RunConfiguration CreateRunConfiguration()
        {
            return new TestToolStartup.RunConfiguration
            {
                Mode = TestToolStartup.RunConfiguration.RunMode.Test,
                OutputWriter = new StringWriter()
            };            
        }
    }
}
