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

using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// A RuntimeApiClient subclass for testing LambdaBootstrap streaming integration.
    /// Extends RuntimeApiClient so the (RuntimeApiClient)Client cast in LambdaBootstrap works.
    /// Overrides StartStreamingResponseAsync to avoid real HTTP calls.
    /// </summary>
    internal class TestStreamingRuntimeApiClient : RuntimeApiClient, IRuntimeApiClient
    {
        private readonly IEnvironmentVariables _environmentVariables;
        private readonly Dictionary<string, IEnumerable<string>> _headers;

        public new IConsoleLoggerWriter ConsoleLogger { get; } = new LogLevelLoggerWriter(new SystemEnvironmentVariables());

        public TestStreamingRuntimeApiClient(IEnvironmentVariables environmentVariables, Dictionary<string, IEnumerable<string>> headers)
            : base(environmentVariables, new NoOpInternalRuntimeApiClient())
        {
            _environmentVariables = environmentVariables;
            _headers = headers;
        }

        // Tracking flags
        public bool GetNextInvocationAsyncCalled { get; private set; }
        public bool ReportInitializationErrorAsyncExceptionCalled { get; private set; }
        public bool ReportInvocationErrorAsyncExceptionCalled { get; private set; }
        public bool SendResponseAsyncCalled { get; private set; }
        public bool StartStreamingResponseAsyncCalled { get; private set; }

        public string LastTraceId { get; private set; }
        public byte[] FunctionInput { get; set; }
        public Stream LastOutputStream { get; private set; }
        public Exception LastRecordedException { get; private set; }
        public ResponseStream LastStreamingResponseStream { get; private set; }

        public new async Task<InvocationRequest> GetNextInvocationAsync(CancellationToken cancellationToken = default)
        {
            GetNextInvocationAsyncCalled = true;

            LastTraceId = Guid.NewGuid().ToString();
            _headers[RuntimeApiHeaders.HeaderTraceId] = new List<string>() { LastTraceId };

            var inputStream = new MemoryStream(FunctionInput == null ? new byte[0] : FunctionInput);
            inputStream.Position = 0;

            return new InvocationRequest()
            {
                InputStream = inputStream,
                LambdaContext = new LambdaContext(
                    new RuntimeApiHeaders(_headers),
                    new LambdaEnvironment(_environmentVariables),
                    new TestDateTimeHelper(), new SimpleLoggerWriter(_environmentVariables))
            };
        }

        public new Task ReportInitializationErrorAsync(Exception exception, String errorType = null, CancellationToken cancellationToken = default)
        {
            LastRecordedException = exception;
            ReportInitializationErrorAsyncExceptionCalled = true;
            return Task.CompletedTask;
        }

        public new Task ReportInitializationErrorAsync(string errorType, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public new Task ReportInvocationErrorAsync(string awsRequestId, Exception exception, CancellationToken cancellationToken = default)
        {
            LastRecordedException = exception;
            ReportInvocationErrorAsyncExceptionCalled = true;
            return Task.CompletedTask;
        }

        public new async Task SendResponseAsync(string awsRequestId, Stream outputStream, CancellationToken cancellationToken = default)
        {
            if (outputStream != null)
            {
                LastOutputStream = new MemoryStream((int)outputStream.Length);
                outputStream.CopyTo(LastOutputStream);
                LastOutputStream.Position = 0;
            }

            SendResponseAsyncCalled = true;
        }

        internal override async Task StartStreamingResponseAsync(
            string awsRequestId, ResponseStream responseStream, CancellationToken cancellationToken = default)
        {
            StartStreamingResponseAsyncCalled = true;
            LastStreamingResponseStream = responseStream;

            // Simulate the HTTP stream being available
            await responseStream.SetHttpOutputStreamAsync(new MemoryStream(), cancellationToken);

            // Wait for the handler to finish writing (mirrors real SerializeToStreamAsync behavior)
            await responseStream.WaitForCompletionAsync();
        }

#if NET8_0_OR_GREATER
        public new Task RestoreNextInvocationAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public new Task ReportRestoreErrorAsync(Exception exception, String errorType = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
#endif
    }
}
