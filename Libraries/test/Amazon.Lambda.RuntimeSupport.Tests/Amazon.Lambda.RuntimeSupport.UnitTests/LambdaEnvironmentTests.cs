/*
 * Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */
using System.Text.RegularExpressions;
using Amazon.Lambda.RuntimeSupport.Bootstrap;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class LambdaEnvironmentTests
    {
        private const string LambdaExecutionEnvironment = "AWS_Lambda_dotnet_custom";
        private readonly TestEnvironmentVariables _environmentVariables;

        public LambdaEnvironmentTests()
        {
            _environmentVariables = new TestEnvironmentVariables();
        }

        [Fact]
        public void SetsExecutionEnvironmentButNotTwice()
        {
            var expectedValueRegex = $"{LambdaExecutionEnvironment}_amazonlambdaruntimesupport_[0-9]+\\.[0-9]+\\.[0-9]+";
            _environmentVariables.SetEnvironmentVariable(LambdaEnvironment.EnvVarExecutionEnvironment, LambdaExecutionEnvironment);

            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables);
            Assert.Matches(expectedValueRegex, lambdaEnvironment.ExecutionEnvironment);
            Assert.Matches(expectedValueRegex, _environmentVariables.GetEnvironmentVariable(LambdaEnvironment.EnvVarExecutionEnvironment));

            // Make sure that creating another LambdaEnvironment instance won't change the value.
            lambdaEnvironment = new LambdaEnvironment(_environmentVariables);
            Assert.Matches(expectedValueRegex, lambdaEnvironment.ExecutionEnvironment);
            Assert.Matches(expectedValueRegex, _environmentVariables.GetEnvironmentVariable(LambdaEnvironment.EnvVarExecutionEnvironment));
        }

        [Fact]
        public void RuntimeApiEndpoint_NotSet()
        {
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables, new LambdaBootstrapOptions());
            Assert.Null(lambdaEnvironment.RuntimeServerHostAndPort);
        }

        [Fact]
        public void RuntimeApiEndpoint_EnvironmentSet()
        {
            _environmentVariables.SetEnvironmentVariable(LambdaEnvironment.EnvVarServerHostAndPort, "testEndpoint");
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables, new LambdaBootstrapOptions());
            Assert.Equal("testEndpoint", lambdaEnvironment.RuntimeServerHostAndPort);
        }

        [Fact]
        public void RuntimeApiEndpoint_OptionsSet()
        {
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables, new LambdaBootstrapOptions {RuntimeApiEndpoint = "testEndpoint" });
            Assert.Equal("testEndpoint", lambdaEnvironment.RuntimeServerHostAndPort);
        }

        [Fact]
        public void RuntimeApiEndpoint_EnvironmentAndOptionsSet()
        {
            _environmentVariables.SetEnvironmentVariable(LambdaEnvironment.EnvVarServerHostAndPort, "testEndpoint");
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables, new LambdaBootstrapOptions {RuntimeApiEndpoint = "priorityEndpoint" });
            Assert.Equal("priorityEndpoint", lambdaEnvironment.RuntimeServerHostAndPort);
        }

        [Fact]
        public void RuntimeApiEndpoint_NotSet_NoBootstrapOptions()
        {
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables);
            Assert.Null(lambdaEnvironment.RuntimeServerHostAndPort);
        }

        [Fact]
        public void RuntimeApiEndpoint_EnvironmentSet_NoBootstrapOptions()
        {
            _environmentVariables.SetEnvironmentVariable(LambdaEnvironment.EnvVarServerHostAndPort, "testEndpoint");
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables);
            Assert.Equal("testEndpoint", lambdaEnvironment.RuntimeServerHostAndPort);
        }

    }
}
