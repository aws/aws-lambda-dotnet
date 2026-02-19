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
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    [Collection("ResponseStreamFactory")]
    public class ResponseStreamFactoryTests : IDisposable
    {
        private const long MaxResponseSize = 20 * 1024 * 1024;

        public void Dispose()
        {
            // Clean up both modes to avoid test pollution
            LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);
            LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: true);
        }

        /// <summary>
        /// A minimal RuntimeApiClient subclass for testing that overrides StartStreamingResponseAsync
        /// to avoid real HTTP calls while tracking invocations.
        /// </summary>
        private class MockStreamingRuntimeApiClient : RuntimeApiClient
        {
            public bool StartStreamingCalled { get; private set; }
            public string LastAwsRequestId { get; private set; }
            public LambdaResponseStream LastResponseStream { get; private set; }
            public TaskCompletionSource<bool> SendTaskCompletion { get; } = new TaskCompletionSource<bool>();

            public MockStreamingRuntimeApiClient()
                : base(new TestEnvironmentVariables(), new TestHelpers.NoOpInternalRuntimeApiClient())
            {
            }

            internal override async Task StartStreamingResponseAsync(
                string awsRequestId, LambdaResponseStream responseStream, CancellationToken cancellationToken = default)
            {
                StartStreamingCalled = true;
                LastAwsRequestId = awsRequestId;
                LastResponseStream = responseStream;
                await SendTaskCompletion.Task;
            }
        }

        private void InitializeWithMock(string requestId, bool isMultiConcurrency, MockStreamingRuntimeApiClient mockClient)
        {
            LambdaResponseStreamFactory.InitializeInvocation(
                requestId, isMultiConcurrency,
                mockClient, CancellationToken.None);
        }

        // --- Property 1: CreateStream Returns Valid Stream ---

        /// <summary>
        /// Property 1: CreateStream Returns Valid Stream - on-demand mode.
        /// Validates: Requirements 1.3, 2.2, 2.3
        /// </summary>
        [Fact]
        public void CreateStream_OnDemandMode_ReturnsValidStream()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-1", isMultiConcurrency: false, mock);

            var stream = LambdaResponseStreamFactory.CreateStream();

            Assert.NotNull(stream);
            Assert.IsAssignableFrom<ILambdaResponseStream>(stream);
        }

        /// <summary>
        /// Property 1: CreateStream Returns Valid Stream - multi-concurrency mode.
        /// Validates: Requirements 1.3, 2.2, 2.3
        /// </summary>
        [Fact]
        public void CreateStream_MultiConcurrencyMode_ReturnsValidStream()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-2", isMultiConcurrency: true, mock);

            var stream = LambdaResponseStreamFactory.CreateStream();

            Assert.NotNull(stream);
            Assert.IsAssignableFrom<ILambdaResponseStream>(stream);
        }

        // --- Property 4: Single Stream Per Invocation ---

        /// <summary>
        /// Property 4: Single Stream Per Invocation - calling CreateStream twice throws.
        /// Validates: Requirements 2.5, 2.6
        /// </summary>
        [Fact]
        public void CreateStream_CalledTwice_ThrowsInvalidOperationException()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-3", isMultiConcurrency: false, mock);
            LambdaResponseStreamFactory.CreateStream();

            Assert.Throws<InvalidOperationException>(() => LambdaResponseStreamFactory.CreateStream());
        }

        [Fact]
        public void CreateStream_OutsideInvocationContext_ThrowsInvalidOperationException()
        {
            // No InitializeInvocation called
            Assert.Throws<InvalidOperationException>(() => LambdaResponseStreamFactory.CreateStream());
        }

        // --- CreateStream starts HTTP POST ---

        /// <summary>
        /// Validates that CreateStream calls StartStreamingResponseAsync on the RuntimeApiClient.
        /// Validates: Requirements 1.3, 1.4, 2.2, 2.3, 2.4
        /// </summary>
        [Fact]
        public void CreateStream_CallsStartStreamingResponseAsync()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-start", isMultiConcurrency: false, mock);

            LambdaResponseStreamFactory.CreateStream();

            Assert.True(mock.StartStreamingCalled);
            Assert.Equal("req-start", mock.LastAwsRequestId);
            Assert.NotNull(mock.LastResponseStream);
        }

        // --- GetSendTask ---

        /// <summary>
        /// Validates that GetSendTask returns the task from the HTTP POST.
        /// Validates: Requirements 5.1, 7.3
        /// </summary>
        [Fact]
        public void GetSendTask_AfterCreateStream_ReturnsNonNullTask()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-send", isMultiConcurrency: false, mock);

            LambdaResponseStreamFactory.CreateStream();

            var sendTask = LambdaResponseStreamFactory.GetSendTask(isMultiConcurrency: false);
            Assert.NotNull(sendTask);
        }

        [Fact]
        public void GetSendTask_BeforeCreateStream_ReturnsNull()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-nosend", isMultiConcurrency: false, mock);

            var sendTask = LambdaResponseStreamFactory.GetSendTask(isMultiConcurrency: false);
            Assert.Null(sendTask);
        }

        [Fact]
        public void GetSendTask_NoContext_ReturnsNull()
        {
            Assert.Null(LambdaResponseStreamFactory.GetSendTask(isMultiConcurrency: false));
        }

        // --- Internal methods ---

        [Fact]
        public void InitializeInvocation_OnDemand_SetsUpContext()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-4", isMultiConcurrency: false, mock);

            Assert.Null(LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false));

            var stream = LambdaResponseStreamFactory.CreateStream();
            Assert.NotNull(stream);
        }

        [Fact]
        public void InitializeInvocation_MultiConcurrency_SetsUpContext()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-5", isMultiConcurrency: true, mock);

            Assert.Null(LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: true));

            var stream = LambdaResponseStreamFactory.CreateStream();
            Assert.NotNull(stream);
        }

        [Fact]
        public void GetStreamIfCreated_AfterCreateStream_ReturnsStream()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-6", isMultiConcurrency: false, mock);
            LambdaResponseStreamFactory.CreateStream();

            var retrieved = LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false);
            Assert.NotNull(retrieved);
        }

        [Fact]
        public void GetStreamIfCreated_NoContext_ReturnsNull()
        {
            Assert.Null(LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false));
        }

        [Fact]
        public void CleanupInvocation_ClearsState()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-7", isMultiConcurrency: false, mock);
            LambdaResponseStreamFactory.CreateStream();

            LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);

            Assert.Null(LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false));
            Assert.Throws<InvalidOperationException>(() => LambdaResponseStreamFactory.CreateStream());
        }

        // --- Property 16: State Isolation Between Invocations ---

        /// <summary>
        /// Property 16: State Isolation Between Invocations - state from one invocation doesn't leak to the next.
        /// Validates: Requirements 6.5, 8.9
        /// </summary>
        [Fact]
        public void StateIsolation_SequentialInvocations_NoLeakage()
        {
            var mock = new MockStreamingRuntimeApiClient();

            // First invocation - streaming
            InitializeWithMock("req-8a", isMultiConcurrency: false, mock);
            var stream1 = LambdaResponseStreamFactory.CreateStream();
            Assert.NotNull(stream1);
            LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);

            // Second invocation - should start fresh
            InitializeWithMock("req-8b", isMultiConcurrency: false, mock);
            Assert.Null(LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false));

            var stream2 = LambdaResponseStreamFactory.CreateStream();
            Assert.NotNull(stream2);
            LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);
        }

        /// <summary>
        /// Property 16: State Isolation - multi-concurrency mode uses AsyncLocal.
        /// Validates: Requirements 2.9, 2.10
        /// </summary>
        [Fact]
        public async Task StateIsolation_MultiConcurrency_UsesAsyncLocal()
        {
            var mock = new MockStreamingRuntimeApiClient();
            InitializeWithMock("req-9", isMultiConcurrency: true, mock);
            var stream = LambdaResponseStreamFactory.CreateStream();
            Assert.NotNull(stream);

            bool childSawNull = false;
            await Task.Run(() =>
            {
                LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: true);
                childSawNull = LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: true) == null;
            });

            Assert.True(childSawNull);
        }
    }
}
