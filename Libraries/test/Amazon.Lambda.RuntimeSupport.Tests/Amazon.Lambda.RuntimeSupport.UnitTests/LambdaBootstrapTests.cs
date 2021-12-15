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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

using Amazon.Lambda.RuntimeSupport.Bootstrap;
using static Amazon.Lambda.RuntimeSupport.Bootstrap.Constants;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// Tests to test LambdaBootstrap when it's constructed using its actual constructor.
    /// Tests of the static GetLambdaBootstrap methods can be found in LambdaBootstrapWrapperTests.
    /// </summary>
    public class LambdaBootstrapTests
    {
        TestHandler _testFunction;
        TestInitializer _testInitializer;
        TestRuntimeApiClient _testRuntimeApiClient;
        TestEnvironmentVariables _environmentVariables;
        HandlerWrapper _testWrapper;

        public LambdaBootstrapTests()
        {
            _environmentVariables = new TestEnvironmentVariables();
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                {
                    RuntimeApiHeaders.HeaderAwsRequestId, new List<string> {"request_id"}
                },
                {
                    RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string> {"invoked_function_arn"}
                }
            };
            _testRuntimeApiClient = new TestRuntimeApiClient(_environmentVariables, headers);
            _testInitializer = new TestInitializer();
            _testFunction = new TestHandler();
            _testWrapper = HandlerWrapper.GetHandlerWrapper(_testFunction.HandlerVoidVoidSync);
        }

        [Fact]
        public void ThrowsExceptionForNullHandler()
        {
            Assert.Throws<ArgumentNullException>("handler", () => { new LambdaBootstrap((LambdaBootstrapHandler)null); });
        }

        [Fact]
        public void ThrowsExceptionForNullHttpClient()
        {
            Assert.Throws<ArgumentNullException>("httpClient", () => { new LambdaBootstrap((HttpClient)null, _testFunction.BaseHandlerAsync); });
            Assert.Throws<ArgumentNullException>("httpClient", () => { new LambdaBootstrap((HttpClient)null, _testWrapper); });
        }

        [Fact]
        public async Task NoInitializer()
        {
            _testFunction.CancellationSource.Cancel();

            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerAsync, null))
            {
                bootstrap.Client = _testRuntimeApiClient;
                await bootstrap.RunAsync(_testFunction.CancellationSource.Token);
            }
            Assert.False(_testInitializer.InitializerWasCalled);
            Assert.False(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public async Task InitializerThrowsException()
        {
            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerAsync, _testInitializer.InitializeThrowAsync))
            {
                bootstrap.Client = _testRuntimeApiClient;
                var exception = await Assert.ThrowsAsync<Exception>(async () => { await bootstrap.RunAsync(); });
                Assert.Equal(TestInitializer.InitializeExceptionMessage, exception.Message);
            }

            Assert.True(_testRuntimeApiClient.ReportInitializationErrorAsyncExceptionCalled);
            Assert.True(_testInitializer.InitializerWasCalled);
            Assert.False(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public async Task InitializerReturnsFalse()
        {
            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerAsync, _testInitializer.InitializeFalseAsync))
            {
                await bootstrap.RunAsync();
            }
            Assert.True(_testInitializer.InitializerWasCalled);
            Assert.False(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public async Task InitializerReturnsTrueAndHandlerLoopRuns()
        {
            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerAsync, _testInitializer.InitializeTrueAsync))
            {
                bootstrap.Client = _testRuntimeApiClient;
                await bootstrap.RunAsync(_testFunction.CancellationSource.Token);
            }

            Assert.True(_testInitializer.InitializerWasCalled);
            Assert.True(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public async Task TraceIdEnvironmentVariableIsSet()
        {
            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerAsync, null))
            {
                bootstrap.Client = _testRuntimeApiClient;
                Assert.Null(_environmentVariables.GetEnvironmentVariable(LambdaEnvironment.EnvVarTraceId));

                await bootstrap.InvokeOnceAsync();

                Assert.NotNull(_testRuntimeApiClient.LastTraceId);
                Assert.Equal(_testRuntimeApiClient.LastTraceId, _environmentVariables.GetEnvironmentVariable(LambdaEnvironment.EnvVarTraceId));
            }

            Assert.False(_testInitializer.InitializerWasCalled);
            Assert.True(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public async Task HandlerThrowsException()
        {
            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerThrowsAsync, null))
            {
                bootstrap.Client = _testRuntimeApiClient;
                Assert.Null(_environmentVariables.GetEnvironmentVariable(LambdaEnvironment.EnvVarTraceId));

                await bootstrap.InvokeOnceAsync();
            }

            Assert.True(_testRuntimeApiClient.ReportInvocationErrorAsyncExceptionCalled);
            Assert.False(_testInitializer.InitializerWasCalled);
            Assert.True(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public async Task HandlerInputAndOutputWork()
        {
            const string testInput = "a MiXeD cAsE sTrInG";

            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerToUpperAsync, null))
            {
                _testRuntimeApiClient.FunctionInput = Encoding.UTF8.GetBytes(testInput);
                bootstrap.Client = _testRuntimeApiClient;
                Assert.Null(_environmentVariables.GetEnvironmentVariable(LambdaEnvironment.EnvVarTraceId));

                await bootstrap.InvokeOnceAsync();
            }

            _testRuntimeApiClient.VerifyOutput(testInput.ToUpper());

                Assert.False(_testInitializer.InitializerWasCalled);
            Assert.True(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public async Task HandlerReturnsNull()
        {
            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerReturnsNullAsync, null))
            {
                _testRuntimeApiClient.FunctionInput = new byte[0];
                bootstrap.Client = _testRuntimeApiClient;
                Assert.Null(_environmentVariables.GetEnvironmentVariable(LambdaEnvironment.EnvVarTraceId));

                await bootstrap.InvokeOnceAsync();
            }

            _testRuntimeApiClient.VerifyOutput((byte[])null);

            Assert.False(_testInitializer.InitializerWasCalled);
            Assert.True(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public void VerifyUserAgentStringSet()
        {
            var client = LambdaBootstrap.ConstructHttpClient();
            var values = client.DefaultRequestHeaders.GetValues("User-Agent");
            Assert.Single(values);

            var userAgent = values.First();
            Assert.StartsWith("aws-lambda-dotnet", userAgent);

            var topLevelTokens = userAgent.Split('/');
            Assert.Equal(2, topLevelTokens.Length);

            var versions = topLevelTokens[1].Split('-');
            Assert.Equal(2, versions.Length);

            var dotnetVersion = Version.Parse(versions[0]);
            Assert.True(Version.Parse("2.0.0") < dotnetVersion);

            var runtimeLambdaSupportVersion = Version.Parse(versions[1]);
            Assert.True(Version.Parse("1.0.0") <= runtimeLambdaSupportVersion);
        }

        [Fact]
        public void IsCallPreJitTest()
        {
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "ProvisionedConcurrency");
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE,
                AWS_LAMBDA_INITIALIZATION_TYPE_PC);

            Assert.True(UserCodeInit.IsCallPreJit());
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "ProvisionedConcurrency");
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE,
                AWS_LAMBDA_INITIALIZATION_TYPE_ON_DEMAND);

            Assert.False(UserCodeInit.IsCallPreJit());

            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "ProvisionedConcurrency");
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.False(UserCodeInit.IsCallPreJit());

            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "Always");
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.True(UserCodeInit.IsCallPreJit());

            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "Never");
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.False(UserCodeInit.IsCallPreJit());

            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, null);
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.False(UserCodeInit.IsCallPreJit());

            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, null);
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, AWS_LAMBDA_INITIALIZATION_TYPE_ON_DEMAND);
            Assert.False(UserCodeInit.IsCallPreJit());

            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "Never");
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.False(UserCodeInit.IsCallPreJit());

            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, null);
            Environment.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, AWS_LAMBDA_INITIALIZATION_TYPE_PC);
            Assert.True(UserCodeInit.IsCallPreJit());
        }
    }
}
