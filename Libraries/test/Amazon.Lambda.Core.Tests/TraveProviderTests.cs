// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace Amazon.Lambda.Core.Tests
{
    public class TraveProviderTests
    {
        [Fact]
        public void SetTraceIdNoMultiConcurrency()
        {
            LambdaTraceProvider.SetCurrentTraceId("trace-id-123");
            Assert.Equal("trace-id-123", LambdaTraceProvider.CurrentTraceId);
        }

        [Fact]
        public async Task SetTraceIdWithMultiConcurrency()
        {
            Environment.SetEnvironmentVariable(LambdaTraceProvider.ENV_VAR_AWS_LAMBDA_MAX_CONCURRENCY, "2");
            try
            {
                var successCount = 0;
                Func<int, string,Task> action = async (sleep, traceId) =>
                {
                    LambdaTraceProvider.SetCurrentTraceId(traceId);
                    await Task.Delay(sleep);
                    Assert.Equal(traceId, LambdaTraceProvider.CurrentTraceId);
                    Interlocked.Increment(ref successCount);
                };

                var tasks = new List<Task>
                {
                    Task.Run(async () => await action(500, "trace-id-1")),
                    Task.Run(async () => await action(200, "trace-id-2")),
                    Task.Run(async () => await action(350, "trace-id-3"))
                };

                await Task.WhenAll(tasks);
                Assert.Equal(3, successCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable(LambdaTraceProvider.ENV_VAR_AWS_LAMBDA_MAX_CONCURRENCY, null);
            }
        }
    }
}
