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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
using Amazon.Lambda.RuntimeSupport.Bootstrap;
using static Amazon.Lambda.RuntimeSupport.Bootstrap.Constants;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// Tests to test LambdaBootstrap when it's constructed using its actual constructor.
    /// Tests of the static GetLambdaBootstrap methods can be found in LambdaBootstrapWrapperTests.
    /// </summary>
    [Collection("ResponseStreamFactory")]
    public class LambdaBootstrapTests
    {
        readonly TestHandler _testFunction;
        readonly TestInitializer _testInitializer;
        readonly TestRuntimeApiClient _testRuntimeApiClient;
        readonly TestEnvironmentVariables _environmentVariables;
        readonly HandlerWrapper _testWrapper;

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
                },
                {
                    RuntimeApiHeaders.HeaderAwsTenantId, new List<string> {"tenant_id"}
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
        public async Task ConfirmRuntimeApiHeadersInContext()
        {

            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerConfirmContextAsync, null))
            {
                bootstrap.Client = _testRuntimeApiClient;
                await bootstrap.RunAsync(_testFunction.CancellationSource.Token);
            }
            Assert.True(_testFunction.HandlerWasCalled);
        }

        [Fact]
        public async Task InitializerHandlesExceptions()
        {
            bool exceptionThrown = false;
            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerAsync, _testInitializer.InitializeThrowAsync))
            {
                bootstrap.Client = _testRuntimeApiClient;
                try
                {
                    await bootstrap.RunAsync();
                }
                catch
                {
                    exceptionThrown = true;
                }
            }

            Assert.True(exceptionThrown);
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
            using (var bootstrap = new LambdaBootstrap(_testFunction.BaseHandlerAsync, null, null, _environmentVariables))
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
            var environmentVariables = new TestEnvironmentVariables();

            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "ProvisionedConcurrency");
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE,
                AWS_LAMBDA_INITIALIZATION_TYPE_PC);

            Assert.True(UserCodeInit.IsCallPreJit(environmentVariables));
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "ProvisionedConcurrency");
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE,
                AWS_LAMBDA_INITIALIZATION_TYPE_ON_DEMAND);

            Assert.False(UserCodeInit.IsCallPreJit(environmentVariables));

            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "ProvisionedConcurrency");
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.False(UserCodeInit.IsCallPreJit(environmentVariables));

            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "Always");
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.True(UserCodeInit.IsCallPreJit(environmentVariables));

            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "Never");
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.False(UserCodeInit.IsCallPreJit(environmentVariables));

            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, null);
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.False(UserCodeInit.IsCallPreJit(environmentVariables));

            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, null);
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, AWS_LAMBDA_INITIALIZATION_TYPE_ON_DEMAND);
            Assert.False(UserCodeInit.IsCallPreJit(environmentVariables));

            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, "Never");
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, null);
            Assert.False(UserCodeInit.IsCallPreJit(environmentVariables));

            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT, null);
            environmentVariables.SetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE, AWS_LAMBDA_INITIALIZATION_TYPE_PC);
            Assert.True(UserCodeInit.IsCallPreJit(environmentVariables));
        }

        // --- Streaming Integration Tests ---

        private TestStreamingRuntimeApiClient CreateStreamingClient()
        {
            var envVars = new TestEnvironmentVariables();
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                { RuntimeApiHeaders.HeaderAwsRequestId, new List<string> { "streaming-request-id" } },
                { RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string> { "invoked_function_arn" } },
                { RuntimeApiHeaders.HeaderAwsTenantId, new List<string> { "tenant_id" } }
            };
            return new TestStreamingRuntimeApiClient(envVars, headers);
        }

        /// <summary>
        /// Property 2: CreateStream Enables Streaming Mode
        /// When a handler calls ResponseStreamFactory.CreateStream(), the response is transmitted
        /// using streaming mode. LambdaBootstrap awaits the send task.
        /// **Validates: Requirements 1.4, 6.1, 6.2, 6.3, 6.4**
        /// </summary>
        [Fact]
        public async Task StreamingMode_HandlerCallsCreateStream_SendTaskAwaited()
        {
            var streamingClient = CreateStreamingClient();

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var stream = ResponseStreamFactory.CreateStream(Array.Empty<byte>());
                await stream.WriteAsync(Encoding.UTF8.GetBytes("hello"));
                return new InvocationResponse(Stream.Null, false);
            };

            using (var bootstrap = new LambdaBootstrap(handler, null))
            {
                bootstrap.Client = streamingClient;
                await bootstrap.InvokeOnceAsync();
            }

            Assert.True(streamingClient.StartStreamingResponseAsyncCalled);
            Assert.False(streamingClient.SendResponseAsyncCalled);
        }

        /// <summary>
        /// Property 3: Default Mode Is Buffered
        /// When a handler does not call ResponseStreamFactory.CreateStream(), the response
        /// is transmitted using buffered mode via SendResponseAsync.
        /// **Validates: Requirements 1.5, 7.2**
        /// </summary>
        [Fact]
        public async Task BufferedMode_HandlerDoesNotCallCreateStream_UsesSendResponse()
        {
            var streamingClient = CreateStreamingClient();

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var outputStream = new MemoryStream(Encoding.UTF8.GetBytes("buffered response"));
                return new InvocationResponse(outputStream);
            };

            using (var bootstrap = new LambdaBootstrap(handler, null))
            {
                bootstrap.Client = streamingClient;
                await bootstrap.InvokeOnceAsync();
            }

            Assert.False(streamingClient.StartStreamingResponseAsyncCalled);
            Assert.True(streamingClient.SendResponseAsyncCalled);
        }

        /// <summary>
        /// Property 14: Exception After Writes Uses Trailers
        /// When a handler throws an exception after writing data to an IResponseStream,
        /// the error is reported via trailers (ReportErrorAsync) rather than standard error reporting.
        /// **Validates: Requirements 5.6, 5.7**
        /// </summary>
        [Fact]
        public async Task MidstreamError_ExceptionAfterWrites_ReportsViaTrailers()
        {
            var streamingClient = CreateStreamingClient();

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var stream = ResponseStreamFactory.CreateStream(Array.Empty<byte>());
                await stream.WriteAsync(Encoding.UTF8.GetBytes("partial data"));
                throw new InvalidOperationException("midstream failure");
            };

            using (var bootstrap = new LambdaBootstrap(handler, null))
            {
                bootstrap.Client = streamingClient;
                await bootstrap.InvokeOnceAsync();
            }

            // Error should be reported via trailers on the stream, not via standard error reporting
            Assert.True(streamingClient.StartStreamingResponseAsyncCalled);
            Assert.NotNull(streamingClient.LastStreamingResponseStream);
            Assert.True(streamingClient.LastStreamingResponseStream.HasError);
            Assert.False(streamingClient.ReportInvocationErrorAsyncExceptionCalled);
        }

        /// <summary>
        /// Property 15: Exception Before CreateStream Uses Standard Error
        /// When a handler throws an exception before calling ResponseStreamFactory.CreateStream(),
        /// the error is reported using the standard Lambda error reporting mechanism.
        /// **Validates: Requirements 5.7, 7.1**
        /// </summary>
        [Fact]
        public async Task PreStreamError_ExceptionBeforeCreateStream_UsesStandardErrorReporting()
        {
            var streamingClient = CreateStreamingClient();

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("pre-stream failure");
            };

            using (var bootstrap = new LambdaBootstrap(handler, null))
            {
                bootstrap.Client = streamingClient;
                await bootstrap.InvokeOnceAsync();
            }

            Assert.False(streamingClient.StartStreamingResponseAsyncCalled);
            Assert.True(streamingClient.ReportInvocationErrorAsyncExceptionCalled);
        }

        /// <summary>
        /// State Isolation: ResponseStreamFactory state is cleared after each invocation.
        /// **Validates: Requirements 6.5, 8.9**
        /// </summary>
        [Fact]
        public async Task Cleanup_ResponseStreamFactoryStateCleared_AfterInvocation()
        {
            var streamingClient = CreateStreamingClient();

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var stream = ResponseStreamFactory.CreateStream(Array.Empty<byte>());
                await stream.WriteAsync(Encoding.UTF8.GetBytes("data"));
                return new InvocationResponse(Stream.Null, false);
            };

            using (var bootstrap = new LambdaBootstrap(handler, null))
            {
                bootstrap.Client = streamingClient;
                await bootstrap.InvokeOnceAsync();
            }

            // After invocation, factory state should be cleaned up
            Assert.Null(ResponseStreamFactory.GetStreamIfCreated(false));
            Assert.Null(ResponseStreamFactory.GetSendTask(false));
        }
    }
}
