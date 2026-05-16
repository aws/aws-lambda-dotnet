// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;
using Amazon.Lambda.Serialization.Json;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class LambdaBootstrapMultiConcurrencyTests
    {
        JsonSerializer _serializer = new JsonSerializer();

        public LambdaBootstrapMultiConcurrencyTests()
        {

        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ConfirmConcurrentInvocations(bool useAsyncHandler)
        {
            TestEnvironmentVariables environmentVariables = new TestEnvironmentVariables();
            environmentVariables.SetEnvironmentVariable(Amazon.Lambda.RuntimeSupport.Bootstrap.Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY, "2");

            var testRuntimeApiClient = new TestMultiConcurrencyRuntimeApiClient(environmentVariables,
                new TestMultiConcurrencyRuntimeApiClient.InvocationEvent
                {
                    Headers = CreateDefaultHeaders("request1", "trace1"),
                    FunctionInput = CreateFunctionInput(new SleepTimeEvent(0, 2000))
                },
                new TestMultiConcurrencyRuntimeApiClient.InvocationEvent
                {
                    Headers = CreateDefaultHeaders("request2", "trace2"),
                    FunctionInput = CreateFunctionInput(new SleepTimeEvent(200, 200))
                }
            );

            var handlerEvents = new List<string>();

            LambdaBootstrapHandler handler;
            if (useAsyncHandler)
            {
                handler = HandlerWrapper.GetHandlerWrapper(async (SleepTimeEvent sleepTime, ILambdaContext context) =>
                {
                    await Task.Delay(sleepTime.StartSleep);

                    lock (handlerEvents)
                    {
                        handlerEvents.Add($"start-{context.AwsRequestId},traceId-{context.TraceId}");
                    }
                    await Task.Delay(sleepTime.ProcessSleep);

                    lock (handlerEvents)
                    {
                        handlerEvents.Add($"end-{context.AwsRequestId},traceId-{context.TraceId}");
                    }
                }, _serializer).Handler;
            }
            else
            {
                handler = HandlerWrapper.GetHandlerWrapper((SleepTimeEvent sleepTime, ILambdaContext context) =>
                {
                    Thread.Sleep(sleepTime.StartSleep);

                    lock (handlerEvents)
                    {
                        handlerEvents.Add($"start-{context.AwsRequestId},traceId-{context.TraceId}");
                    }
                    Thread.Sleep(sleepTime.ProcessSleep);

                    lock (handlerEvents)
                    {
                        handlerEvents.Add($"end-{context.AwsRequestId},traceId-{context.TraceId}");
                    }
                }, _serializer).Handler;
            }


            var lambdaBootstrap = new LambdaBootstrap(
                httpClient: null,
                handler: handler,
                initializer: null,
                ownsHttpClient: true,
                environmentVariables: environmentVariables);
            lambdaBootstrap.Client = testRuntimeApiClient;

            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(3));
                await lambdaBootstrap.RunAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when the cancellation token is triggered.
            }

            Assert.Equal(2, testRuntimeApiClient.ProcessInvocationEvents.Count);
            Assert.Empty(testRuntimeApiClient.InvocationEvents);
            Assert.Equal(4, handlerEvents.Count);

            Assert.Equal("start-request1,traceId-trace1", handlerEvents[0]);
            Assert.Equal("start-request2,traceId-trace2", handlerEvents[1]);
            Assert.Equal("end-request2,traceId-trace2", handlerEvents[2]);
            Assert.Equal("end-request1,traceId-trace1", handlerEvents[3]);
        }

        /// <summary>
        /// Bug condition exploration test: Demonstrates thread pool starvation when MC is enabled
        /// with blocking handlers. This test encodes the EXPECTED behavior after the fix is applied.
        /// On unfixed code, this test is EXPECTED TO FAIL with a timeout because:
        /// - Only 2 polling tasks are created (Math.Max(2, processorCount))
        /// - ThreadPool.MinThreads is constrained to 2 worker threads
        /// - 10 blocking handlers (Thread.Sleep) exhaust the thread pool
        /// - Polling task continuations cannot resume to call GetNextInvocationAsync
        /// - Not all 10 invocations get dequeued within the timeout
        ///
        /// Validates: Requirements 1.1, 1.2, 1.3, 1.4
        /// </summary>
        [Fact]
        public async Task ThreadPoolStarvation_BlockingHandlers_AllInvocationsDequeued()
        {
            // Save original ThreadPool settings to restore after test
            ThreadPool.GetMinThreads(out int originalMinWorker, out int originalMinIO);

            try
            {
                // Constrain ThreadPool to simulate Lambda's default environment (low thread count)
                ThreadPool.SetMinThreads(2, 2);

                TestEnvironmentVariables environmentVariables = new TestEnvironmentVariables();
                environmentVariables.SetEnvironmentVariable(
                    Amazon.Lambda.RuntimeSupport.Bootstrap.Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY, "10");

                // Create 10 invocation events with blocking handlers
                var invocationEvents = new TestMultiConcurrencyRuntimeApiClient.InvocationEvent[10];
                for (int i = 0; i < 10; i++)
                {
                    invocationEvents[i] = new TestMultiConcurrencyRuntimeApiClient.InvocationEvent
                    {
                        Headers = CreateDefaultHeaders($"request{i}", $"trace{i}"),
                        FunctionInput = CreateFunctionInput(new SleepTimeEvent(3000, 0))
                    };
                }

                var testRuntimeApiClient = new TestMultiConcurrencyRuntimeApiClient(environmentVariables, invocationEvents);

                // Use a thread-safe counter to track dequeued invocations
                int dequeuedCount = 0;
                var allDequeuedEvent = new ManualResetEventSlim(false);

                // Wrap the test client to track dequeue operations in a thread-safe manner
                var originalGetNext = testRuntimeApiClient;

                // Handler that performs blocking work (Thread.Sleep) to exhaust the thread pool
                var handler = HandlerWrapper.GetHandlerWrapper((SleepTimeEvent sleepTime, ILambdaContext context) =>
                {
                    // Blocking sleep to simulate CPU-bound or synchronous I/O work
                    Thread.Sleep(sleepTime.StartSleep);
                }, _serializer).Handler;

                var lambdaBootstrap = new LambdaBootstrap(
                    httpClient: null,
                    handler: handler,
                    initializer: null,
                    ownsHttpClient: true,
                    environmentVariables: environmentVariables);
                lambdaBootstrap.Client = testRuntimeApiClient;

                // Run with a 10-second timeout - if all 10 invocations are dequeued, the test passes
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                try
                {
                    await lambdaBootstrap.RunAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when the cancellation token is triggered (timeout)
                }

                // Assert that all 10 invocations were dequeued from the test client within the timeout.
                // On unfixed code, this will fail because thread pool starvation prevents polling tasks
                // from cycling back to GetNextInvocationAsync.
                Assert.Equal(10, testRuntimeApiClient.ProcessInvocationEvents.Count);
            }
            finally
            {
                // Restore original ThreadPool settings
                ThreadPool.SetMinThreads(originalMinWorker, originalMinIO);
            }
        }

        private Dictionary<string, IEnumerable<string>> CreateDefaultHeaders(string requestId, string traceId)
        {
            return new Dictionary<string, IEnumerable<string>>
            {
                {
                    RuntimeApiHeaders.HeaderAwsRequestId, new List<string> { requestId }
                },
                {
                    RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string> {"invoked_function_arn"}
                },
                {
                    RuntimeApiHeaders.HeaderTraceId, new List<string> { traceId }
                }
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

        public class  SleepTimeEvent
        {
            public int StartSleep { get; set; }

            public int ProcessSleep { get; set; }

            public SleepTimeEvent(int startSleep, int processSleep)
            {
                StartSleep = startSleep;
                ProcessSleep = processSleep;
            }
        }
    }
}
