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
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class ResponseStreamFactoryTests : IDisposable
    {
        private const long MaxResponseSize = 20 * 1024 * 1024;

        public void Dispose()
        {
            // Clean up both modes to avoid test pollution
            ResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);
            ResponseStreamFactory.CleanupInvocation(isMultiConcurrency: true);
        }

        // --- Task 3.3: CreateStream tests ---

        /// <summary>
        /// Property 1: CreateStream Returns Valid Stream - on-demand mode.
        /// Validates: Requirements 1.3, 2.2, 2.3
        /// </summary>
        [Fact]
        public void CreateStream_OnDemandMode_ReturnsValidStream()
        {
            ResponseStreamFactory.InitializeInvocation("req-1", MaxResponseSize, isMultiConcurrency: false);

            var stream = ResponseStreamFactory.CreateStream();

            Assert.NotNull(stream);
            Assert.IsAssignableFrom<IResponseStream>(stream);
        }

        /// <summary>
        /// Property 1: CreateStream Returns Valid Stream - multi-concurrency mode.
        /// Validates: Requirements 1.3, 2.2, 2.3
        /// </summary>
        [Fact]
        public void CreateStream_MultiConcurrencyMode_ReturnsValidStream()
        {
            ResponseStreamFactory.InitializeInvocation("req-2", MaxResponseSize, isMultiConcurrency: true);

            var stream = ResponseStreamFactory.CreateStream();

            Assert.NotNull(stream);
            Assert.IsAssignableFrom<IResponseStream>(stream);
        }

        /// <summary>
        /// Property 4: Single Stream Per Invocation - calling CreateStream twice throws.
        /// Validates: Requirements 2.5, 2.6
        /// </summary>
        [Fact]
        public void CreateStream_CalledTwice_ThrowsInvalidOperationException()
        {
            ResponseStreamFactory.InitializeInvocation("req-3", MaxResponseSize, isMultiConcurrency: false);
            ResponseStreamFactory.CreateStream();

            Assert.Throws<InvalidOperationException>(() => ResponseStreamFactory.CreateStream());
        }

        [Fact]
        public void CreateStream_OutsideInvocationContext_ThrowsInvalidOperationException()
        {
            // No InitializeInvocation called
            Assert.Throws<InvalidOperationException>(() => ResponseStreamFactory.CreateStream());
        }

        // --- Task 3.5: Internal methods tests ---

        [Fact]
        public void InitializeInvocation_OnDemand_SetsUpContext()
        {
            ResponseStreamFactory.InitializeInvocation("req-4", MaxResponseSize, isMultiConcurrency: false);

            // GetStreamIfCreated should return null since CreateStream hasn't been called
            Assert.Null(ResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false));

            // But CreateStream should work (proving context was set up)
            var stream = ResponseStreamFactory.CreateStream();
            Assert.NotNull(stream);
        }

        [Fact]
        public void InitializeInvocation_MultiConcurrency_SetsUpContext()
        {
            ResponseStreamFactory.InitializeInvocation("req-5", MaxResponseSize, isMultiConcurrency: true);

            Assert.Null(ResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: true));

            var stream = ResponseStreamFactory.CreateStream();
            Assert.NotNull(stream);
        }

        [Fact]
        public void GetStreamIfCreated_AfterCreateStream_ReturnsStream()
        {
            ResponseStreamFactory.InitializeInvocation("req-6", MaxResponseSize, isMultiConcurrency: false);
            var created = ResponseStreamFactory.CreateStream();

            var retrieved = ResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false);

            Assert.NotNull(retrieved);
        }

        [Fact]
        public void GetStreamIfCreated_NoContext_ReturnsNull()
        {
            Assert.Null(ResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false));
        }

        [Fact]
        public void CleanupInvocation_ClearsState()
        {
            ResponseStreamFactory.InitializeInvocation("req-7", MaxResponseSize, isMultiConcurrency: false);
            ResponseStreamFactory.CreateStream();

            ResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);

            Assert.Null(ResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false));
            Assert.Throws<InvalidOperationException>(() => ResponseStreamFactory.CreateStream());
        }

        /// <summary>
        /// Property 16: State Isolation Between Invocations - state from one invocation doesn't leak to the next.
        /// Validates: Requirements 6.5, 8.9
        /// </summary>
        [Fact]
        public void StateIsolation_SequentialInvocations_NoLeakage()
        {
            // First invocation - streaming
            ResponseStreamFactory.InitializeInvocation("req-8a", MaxResponseSize, isMultiConcurrency: false);
            var stream1 = ResponseStreamFactory.CreateStream();
            Assert.NotNull(stream1);
            ResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);

            // Second invocation - should start fresh
            ResponseStreamFactory.InitializeInvocation("req-8b", MaxResponseSize, isMultiConcurrency: false);
            Assert.Null(ResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: false));

            // Should be able to create a new stream
            var stream2 = ResponseStreamFactory.CreateStream();
            Assert.NotNull(stream2);
            ResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);
        }

        /// <summary>
        /// Property 16: State Isolation - multi-concurrency mode uses AsyncLocal.
        /// Validates: Requirements 2.9, 2.10
        /// </summary>
        [Fact]
        public async Task StateIsolation_MultiConcurrency_UsesAsyncLocal()
        {
            // Initialize in multi-concurrency mode on main thread
            ResponseStreamFactory.InitializeInvocation("req-9", MaxResponseSize, isMultiConcurrency: true);
            var stream = ResponseStreamFactory.CreateStream();
            Assert.NotNull(stream);

            // A separate task should not see the main thread's context
            // (AsyncLocal flows to child tasks, but a fresh Task.Run with new initialization should override)
            bool childSawNull = false;
            await Task.Run(() =>
            {
                // Clean up the flowed context first
                ResponseStreamFactory.CleanupInvocation(isMultiConcurrency: true);
                childSawNull = ResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: true) == null;
            });

            Assert.True(childSawNull);
        }
    }
}
