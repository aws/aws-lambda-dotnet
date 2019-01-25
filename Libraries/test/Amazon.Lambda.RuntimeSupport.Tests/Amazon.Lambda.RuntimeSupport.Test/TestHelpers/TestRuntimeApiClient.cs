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
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.Test
{
    public class TestRuntimeApiClient : IRuntimeApiClient
    {
        private IEnvironmentVariables _environmentVariables;
        private Dictionary<string, IEnumerable<string>> _headers;

        public TestRuntimeApiClient()
        {
            _environmentVariables = new TestEnvironmentVariables();
            _headers = new Dictionary<string, IEnumerable<string>>();
            _headers.Add(RuntimeApiHeaders.HeaderAwsRequestId, new List<string>() { "request_id" });
            _headers.Add(RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string>() { "invoked_function_arn" });
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

        public void VerifyOutput(string expectedOutput)
        {
            VerifyOutput(Encoding.UTF8.GetBytes(expectedOutput));
        }

        public void VerifyOutput(byte[] expectedOutput)
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

        public Task<InvocationRequest> GetNextInvocationAsync()
        {
            GetNextInvocationAsyncCalled = true;

            LastTraceId = Guid.NewGuid().ToString();
            _headers[RuntimeApiHeaders.HeaderTraceId] = new List<string>() { LastTraceId };

            var environmentVariables = new TestEnvironmentVariables();

            var inputStream = new MemoryStream(FunctionInput == null ? new byte[0] : FunctionInput);
            inputStream.Position = 0;

            return Task.FromResult(new InvocationRequest()
            {
                InputStream = inputStream,
                LambdaContext = new LambdaContext(
                    new RuntimeApiHeaders(_headers),
                    new LambdaEnvironment(_environmentVariables))
            });
        }

        public Task ReportInitializationErrorAsync(Exception exception)
        {
            ReportInitializationErrorAsyncExceptionCalled = true;
            return Task.Run(() => { });
        }

        public Task ReportInitializationErrorAsync(string errorType)
        {
            ReportInitializationErrorAsyncTypeCalled = true;
            return Task.Run(() => { });
        }

        public Task ReportInvocationErrorAsync(string awsRequestId, Exception exception)
        {
            ReportInvocationErrorAsyncExceptionCalled = true;
            return Task.Run(() => { });
        }

        public Task ReportInvocationErrorAsync(string awsRequestId, string errorType)
        {
            ReportInvocationErrorAsyncTypeCalled = true;
            return Task.Run(() => { });
        }

        public Task SendResponseAsync(string awsRequestId, Stream outputStream)
        {
            // copy the stream because it gets disposed by the bootstrap
            LastOutputStream = new MemoryStream((int)outputStream.Length);
            outputStream.CopyTo(LastOutputStream);
            LastOutputStream.Position = 0;

            SendResponseAsyncCalled = true;
            return Task.Run(() => { });
        }
    }
}
