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
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    internal class TestRuntimeApiClient : IRuntimeApiClient
    {
        private IEnvironmentVariables _environmentVariables;
        private Dictionary<string, IEnumerable<string>> _headers;

        public TestRuntimeApiClient(IEnvironmentVariables environmentVariables, Dictionary<string, IEnumerable<string>> headers)
        {
            _environmentVariables = environmentVariables;
            _headers = headers;
        }

        public bool GetNextInvocationAsyncCalled { get; private set; }
        public bool ReportInitializationErrorAsyncExceptionCalled { get; private set; }
        public bool ReportInitializationErrorAsyncTypeCalled { get; private set; }
        public bool ReportInvocationErrorAsyncExceptionCalled { get; private set; }
        public bool ReportInvocationErrorAsyncTypeCalled { get; private set; }
        public bool SendResponseAsyncCalled { get; private set; }

        public string LastTraceId { get; private set; }
        public byte[] FunctionInput { get; set; }
        public Stream LastOutputStream { get; private set; }
        public Exception LastRecordedException { get; private set; }

        public void VerifyOutput(string expectedOutput)
        {
            VerifyOutput(Encoding.UTF8.GetBytes(expectedOutput));
        }

        public void VerifyOutput(byte[] expectedOutput)
        {
            if (expectedOutput == null && LastOutputStream == null)
            {
                return;
            }
            else if (expectedOutput != null && LastOutputStream != null)
            {
                int nextByte = 0;
                int count = 0;
                while ((nextByte = LastOutputStream.ReadByte()) != -1)
                {
                    Assert.Equal(expectedOutput[count++], nextByte);
                }
                if (count == 0)
                {
                    Assert.Null(expectedOutput);
                }
            }
            else
            {
                throw new Exception("expectedOutput and LastOutputStream must both be null or both be non-null.");
            }
        }

        public Task<InvocationRequest> GetNextInvocationAsync(CancellationToken cancellationToken = default)
        {
            GetNextInvocationAsyncCalled = true;

            LastTraceId = Guid.NewGuid().ToString();
            _headers[RuntimeApiHeaders.HeaderTraceId] = new List<string>() { LastTraceId };

            var inputStream = new MemoryStream(FunctionInput == null ? new byte[0] : FunctionInput);
            inputStream.Position = 0;

            return Task.FromResult(new InvocationRequest()
            {
                InputStream = inputStream,
                LambdaContext = new LambdaContext(
                    new RuntimeApiHeaders(_headers),
                    new LambdaEnvironment(_environmentVariables),
                    new TestDateTimeHelper(), new Helpers.SimpleLoggerWriter())
            });
        }

        public Task ReportInitializationErrorAsync(Exception exception, CancellationToken cancellationToken = default)
        {
            LastRecordedException = exception;
            ReportInitializationErrorAsyncExceptionCalled = true;
            return Task.Run(() => { });
        }

        public Task ReportInitializationErrorAsync(string errorType, CancellationToken cancellationToken = default)
        {
            ReportInitializationErrorAsyncTypeCalled = true;
            return Task.Run(() => { });
        }

        public Task ReportInvocationErrorAsync(string awsRequestId, Exception exception, CancellationToken cancellationToken = default)
        {
            LastRecordedException = exception;
            ReportInvocationErrorAsyncExceptionCalled = true;
            return Task.Run(() => { });
        }

        public Task ReportInvocationErrorAsync(string awsRequestId, string errorType, CancellationToken cancellationToken = default)
        {
            ReportInvocationErrorAsyncTypeCalled = true;
            return Task.Run(() => { });
        }

        public Task SendResponseAsync(string awsRequestId, Stream outputStream, CancellationToken cancellationToken = default)
        {
            if (outputStream != null)
            {
                // copy the stream because it gets disposed by the bootstrap
                LastOutputStream = new MemoryStream((int)outputStream.Length);
                outputStream.CopyTo(LastOutputStream);
                LastOutputStream.Position = 0;
            }

            SendResponseAsyncCalled = true;
            return Task.Run(() => { });
        }
    }
}
