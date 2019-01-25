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
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.Test
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

        public LambdaBootstrapTests()
        {
            _testRuntimeApiClient = new TestRuntimeApiClient();
            _testInitializer = new TestInitializer();
            _testFunction = new TestHandler();
            _environmentVariables = new TestEnvironmentVariables();
        }

        [Fact]
        public void ThrowsExceptionForNullHandler()
        {
            Assert.Throws<ArgumentNullException>("handler", () => { new LambdaBootstrap(null); });
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
                bootstrap.EnvironmentVariables = _environmentVariables;
                Assert.Null(_environmentVariables.GetEnvironmentVariable(LambdaBootstrap.TraceIdEnvVar));

                await bootstrap.InvokeOnceAsync();

                Assert.NotNull(_testRuntimeApiClient.LastTraceId);
                Assert.Equal(_testRuntimeApiClient.LastTraceId, _environmentVariables.GetEnvironmentVariable(LambdaBootstrap.TraceIdEnvVar));
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
                bootstrap.EnvironmentVariables = _environmentVariables;
                Assert.Null(_environmentVariables.GetEnvironmentVariable(LambdaBootstrap.TraceIdEnvVar));

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
                bootstrap.EnvironmentVariables = _environmentVariables;
                Assert.Null(_environmentVariables.GetEnvironmentVariable(LambdaBootstrap.TraceIdEnvVar));

                await bootstrap.InvokeOnceAsync();
            }

            _testRuntimeApiClient.VerifyOutput(testInput.ToUpper());

                Assert.False(_testInitializer.InitializerWasCalled);
            Assert.True(_testFunction.HandlerWasCalled);
        }
    }
}
