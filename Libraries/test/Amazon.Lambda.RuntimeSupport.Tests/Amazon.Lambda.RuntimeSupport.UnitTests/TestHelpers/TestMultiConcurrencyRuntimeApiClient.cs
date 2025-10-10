// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.RuntimeSupport.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers
{
    internal class TestMultiConcurrencyRuntimeApiClient : IRuntimeApiClient
    {
        private readonly IEnvironmentVariables _environmentVariables;

        public Queue<InvocationEvent> InvocationEvents { get; } = new Queue<InvocationEvent>();
        public Dictionary<string, InvocationEvent> ProcessInvocationEvents { get; } = new Dictionary<string, InvocationEvent>();

        public TestMultiConcurrencyRuntimeApiClient(IEnvironmentVariables environmentVariables, params InvocationEvent[] invocationEvents)
        {
            _environmentVariables = environmentVariables;
            foreach (var invocationEvent in invocationEvents)
            {
                InvocationEvents.Enqueue(invocationEvent);
            }

            ConsoleLogger = new LogLevelLoggerWriter(environmentVariables);
        }

        public IConsoleLoggerWriter ConsoleLogger { get; }

        public class InvocationEvent
        {
            public Dictionary<string, IEnumerable<string>> Headers { get; init; }
            public byte[] FunctionInput { get; init; }

            public Stream OutputStream { get; set; }

            public bool Complete { get; set; }

            public string AwsRequestId
            {
                get
                {
                    if (Headers != null &&
                        Headers.TryGetValue(RuntimeApiHeaders.HeaderAwsRequestId, out var values))
                    {
                        return values?.FirstOrDefault();
                    }
                    return null;
                }
            }
        }

        public async Task<InvocationRequest> GetNextInvocationAsync(CancellationToken cancellationToken = default)
        {
            // If InvocationEvents is empty then all of the test events have been processed.
            // At this point we just need to wait for the test verification to run and then the
            // cancellationToken will be triggered to end delay.
            if (InvocationEvents.Count == 0)
            {
                await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
            }

            var data = InvocationEvents.Dequeue();
            ProcessInvocationEvents[data.AwsRequestId] = data;

            var inputStream = new MemoryStream(data.FunctionInput == null ? new byte[0] : data.FunctionInput);
            inputStream.Position = 0;

            return new InvocationRequest()
            {
                InputStream = inputStream,
                LambdaContext = new LambdaContext(
                    new RuntimeApiHeaders(data.Headers),
                    new LambdaEnvironment(_environmentVariables),
                    new TestDateTimeHelper(), new Helpers.SimpleLoggerWriter(_environmentVariables))
            };
        }

        public Task SendResponseAsync(string awsRequestId, Stream outputStream, CancellationToken cancellationToken = default)
        {
            var data = ProcessInvocationEvents[awsRequestId];
            data.Complete = true;
            if (outputStream != null)
            {
                // copy the stream because it gets disposed by the bootstrap
                data.OutputStream = new MemoryStream((int)outputStream.Length);
                outputStream.CopyTo(data.OutputStream);
                data.OutputStream.Position = 0;
            }

            return Task.Run(() => { });
        }


        public Task RestoreNextInvocationAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => { });
        }

        public Task ReportInitializationErrorAsync(Exception exception, String errorType = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => { });
        }

        public Task ReportInitializationErrorAsync(string errorType, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => { });
        }

        public Task ReportInvocationErrorAsync(string awsRequestId, Exception exception, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => { });
        }

        public Task ReportInvocationErrorAsync(string awsRequestId, string errorType, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => { });
        }

        public Task ReportRestoreErrorAsync(Exception exception, String errorType = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => { });
        }
    }
}
