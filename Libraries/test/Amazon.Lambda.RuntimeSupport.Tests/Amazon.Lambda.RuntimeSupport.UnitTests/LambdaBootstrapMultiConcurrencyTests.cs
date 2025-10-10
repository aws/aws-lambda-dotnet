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
