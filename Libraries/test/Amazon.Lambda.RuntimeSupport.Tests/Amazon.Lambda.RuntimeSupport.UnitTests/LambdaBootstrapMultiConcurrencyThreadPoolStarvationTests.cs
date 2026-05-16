// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;
using Amazon.Lambda.Serialization.Json;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// Tests for multi-concurrency thread pool starvation fix.
    ///
    /// The fix ensures that when AWS_LAMBDA_MAX_CONCURRENCY is set:
    /// 1. The polling task count matches the MC value (not just Math.Max(2, processorCount))
    /// 2. ThreadPool.SetMinThreads is called to pre-size the pool for handler threads + polling continuations
    ///
    /// Without both changes, blocking handlers (Thread.Sleep, .Result, .Wait()) exhaust the
    /// ThreadPool, preventing polling tasks from cycling back to /next.
    /// </summary>
    public class LambdaBootstrapMultiConcurrencyThreadPoolStarvationTests
    {
        private readonly JsonSerializer _serializer = new JsonSerializer();

        /// <summary>
        /// Verifies the fix works: with AdjustThreadPoolSettings pre-sizing the pool
        /// and DetermineProcessingTaskCount using the MC value, all invocations are
        /// dequeued promptly even with blocking handlers.
        ///
        /// This simulates the real Lambda environment where MaxThreads is not capped
        /// but MinThreads starts low. The fix raises MinThreads so threads are
        /// immediately available for both handlers and polling continuations.
        /// </summary>
        [Fact]
        public async Task MultiConcurrency_BlockingHandlers_AllInvocationsDequeued()
        {
            const int mcCount = 10;
            const int invocationCount = 10;
            const int handlerBlockTimeMs = 3000;
            var timeout = TimeSpan.FromSeconds(5);

            var environmentVariables = new TestEnvironmentVariables();
            environmentVariables.SetEnvironmentVariable(
                Amazon.Lambda.RuntimeSupport.Bootstrap.Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY,
                mcCount.ToString());

            var invocationEvents = new TestMultiConcurrencyRuntimeApiClient.InvocationEvent[invocationCount];
            for (int i = 0; i < invocationCount; i++)
            {
                invocationEvents[i] = new TestMultiConcurrencyRuntimeApiClient.InvocationEvent
                {
                    Headers = CreateDefaultHeaders($"request{i + 1}", $"trace{i + 1}"),
                    FunctionInput = CreateFunctionInput(new BlockingEvent { BlockTimeMs = handlerBlockTimeMs })
                };
            }

            var testRuntimeApiClient = new TestMultiConcurrencyRuntimeApiClient(environmentVariables, invocationEvents);
            var startedInvocations = new ConcurrentBag<string>();

            var handler = HandlerWrapper.GetHandlerWrapper((BlockingEvent input, ILambdaContext context) =>
            {
                startedInvocations.Add(context.AwsRequestId);
                Thread.Sleep(input.BlockTimeMs);
            }, _serializer).Handler;

            var lambdaBootstrap = new LambdaBootstrap(
                httpClient: null,
                handler: handler,
                initializer: null,
                ownsHttpClient: true,
                environmentVariables: environmentVariables);
            lambdaBootstrap.Client = testRuntimeApiClient;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            try
            {
                await lambdaBootstrap.RunAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected — the bootstrap runs until cancelled
            }

            // With the fix: 10 polling tasks + pre-sized ThreadPool = all invocations dequeued
            Assert.Equal(invocationCount, testRuntimeApiClient.ProcessInvocationEvents.Count);
            Assert.Equal(invocationCount, startedInvocations.Count);
        }

        /// <summary>
        /// Verifies the fix works at higher concurrency (MC=20).
        /// </summary>
        [Fact]
        public async Task MultiConcurrency_HigherConcurrency_AllInvocationsDequeued()
        {
            const int mcCount = 20;
            const int invocationCount = 20;
            const int handlerBlockTimeMs = 2000;
            var timeout = TimeSpan.FromSeconds(5);

            var environmentVariables = new TestEnvironmentVariables();
            environmentVariables.SetEnvironmentVariable(
                Amazon.Lambda.RuntimeSupport.Bootstrap.Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY,
                mcCount.ToString());

            var invocationEvents = new TestMultiConcurrencyRuntimeApiClient.InvocationEvent[invocationCount];
            for (int i = 0; i < invocationCount; i++)
            {
                invocationEvents[i] = new TestMultiConcurrencyRuntimeApiClient.InvocationEvent
                {
                    Headers = CreateDefaultHeaders($"request{i + 1}", $"trace{i + 1}"),
                    FunctionInput = CreateFunctionInput(new BlockingEvent { BlockTimeMs = handlerBlockTimeMs })
                };
            }

            var testRuntimeApiClient = new TestMultiConcurrencyRuntimeApiClient(environmentVariables, invocationEvents);
            var startedInvocations = new ConcurrentBag<string>();

            var handler = HandlerWrapper.GetHandlerWrapper((BlockingEvent input, ILambdaContext context) =>
            {
                startedInvocations.Add(context.AwsRequestId);
                Thread.Sleep(input.BlockTimeMs);
            }, _serializer).Handler;

            var lambdaBootstrap = new LambdaBootstrap(
                httpClient: null,
                handler: handler,
                initializer: null,
                ownsHttpClient: true,
                environmentVariables: environmentVariables);
            lambdaBootstrap.Client = testRuntimeApiClient;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            try
            {
                await lambdaBootstrap.RunAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            Assert.Equal(invocationCount, testRuntimeApiClient.ProcessInvocationEvents.Count);
            Assert.Equal(invocationCount, startedInvocations.Count);
        }

        /// <summary>
        /// Verifies that non-numeric AWS_LAMBDA_MAX_CONCURRENCY still works
        /// (falls back to Math.Max(2, processorCount) for polling tasks, no ThreadPool adjustment).
        /// </summary>
        [Fact]
        public async Task MultiConcurrency_NonNumericMcValue_FallsBackToProcessorCount()
        {
            const int invocationCount = 2;
            const int handlerBlockTimeMs = 500;
            var timeout = TimeSpan.FromSeconds(3);

            var environmentVariables = new TestEnvironmentVariables();
            // Non-numeric value — MC is enabled but value can't be parsed
            environmentVariables.SetEnvironmentVariable(
                Amazon.Lambda.RuntimeSupport.Bootstrap.Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY,
                "enabled");

            var invocationEvents = new TestMultiConcurrencyRuntimeApiClient.InvocationEvent[invocationCount];
            for (int i = 0; i < invocationCount; i++)
            {
                invocationEvents[i] = new TestMultiConcurrencyRuntimeApiClient.InvocationEvent
                {
                    Headers = CreateDefaultHeaders($"request{i + 1}", $"trace{i + 1}"),
                    FunctionInput = CreateFunctionInput(new BlockingEvent { BlockTimeMs = handlerBlockTimeMs })
                };
            }

            var testRuntimeApiClient = new TestMultiConcurrencyRuntimeApiClient(environmentVariables, invocationEvents);
            var startedInvocations = new ConcurrentBag<string>();

            var handler = HandlerWrapper.GetHandlerWrapper((BlockingEvent input, ILambdaContext context) =>
            {
                startedInvocations.Add(context.AwsRequestId);
                Thread.Sleep(input.BlockTimeMs);
            }, _serializer).Handler;

            var lambdaBootstrap = new LambdaBootstrap(
                httpClient: null,
                handler: handler,
                initializer: null,
                ownsHttpClient: true,
                environmentVariables: environmentVariables);
            lambdaBootstrap.Client = testRuntimeApiClient;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            try
            {
                await lambdaBootstrap.RunAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Should still process both invocations (fallback behavior works)
            Assert.Equal(invocationCount, testRuntimeApiClient.ProcessInvocationEvents.Count);
            Assert.Equal(invocationCount, startedInvocations.Count);
        }

        #region Helper Methods

        private Dictionary<string, IEnumerable<string>> CreateDefaultHeaders(string requestId, string traceId)
        {
            return new Dictionary<string, IEnumerable<string>>
            {
                { RuntimeApiHeaders.HeaderAwsRequestId, new List<string> { requestId } },
                { RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string> { "invoked_function_arn" } },
                { RuntimeApiHeaders.HeaderTraceId, new List<string> { traceId } }
            };
        }

        private byte[] CreateFunctionInput(object input)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                _serializer.Serialize(input, ms);
                return ms.ToArray();
            }
        }

        #endregion

        #region Test Models

        public class BlockingEvent
        {
            public int BlockTimeMs { get; set; }
        }

        #endregion
    }
}
