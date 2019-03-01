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
using Amazon.Lambda.Serialization.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class TestHandler
    {
        public const string InvokeExceptionMessage = "Invoke Exception";

        protected JsonSerializer _jsonSerializer = new JsonSerializer();

        public bool HandlerWasCalled { get; protected set; }
        public byte[] LastInputReceived { get; private set; }
        public byte[] LastOutputSent { get; private set; }
        public CancellationTokenSource CancellationSource { get; } = new CancellationTokenSource();

        public TestHandler()
        {
            // In case something goes wrong make sure tests won't hang forever in the invoke loop.
            CancellationSource.CancelAfter(30000);
        }

        public async Task<InvocationResponse> BaseHandlerAsync(InvocationRequest invocation)
        {
            var outputStream = new MemoryStream();
            await DoHandlerCommonTasksAsync(invocation.InputStream, outputStream);
            return new InvocationResponse(outputStream);
        }

        public async Task<InvocationResponse> BaseHandlerThrowsAsync(InvocationRequest invocation)
        {
            var outputStream = new MemoryStream();
            await DoHandlerCommonTasksAsync(invocation.InputStream, null);
            throw new Exception(InvokeExceptionMessage);
        }

        public async Task<InvocationResponse> BaseHandlerToUpperAsync(InvocationRequest invocation)
        {
            using (var sr = new StreamReader(invocation.InputStream))
            {
                Stream outputStream = new MemoryStream(Encoding.UTF8.GetBytes(sr.ReadToEnd().ToUpper()));
                outputStream.Position = 0;
                await DoHandlerCommonTasksAsync(invocation.InputStream, outputStream);
                return new InvocationResponse(outputStream);
            }
        }

        public async Task<InvocationResponse> BaseHandlerReturnsNullAsync(InvocationRequest invocation)
        {
            using (var sr = new StreamReader(invocation.InputStream))
            {
                await DoHandlerCommonTasksAsync(invocation.InputStream, null);
                return null;
            }
        }

        public void HandlerVoidVoidSync()
        {
            DoHandlerTasks(null, null);
        }

        private async Task DoHandlerCommonTasksAsync(Stream input, Stream output)
        {
            await Task.Delay(0);
            DoHandlerTasks(input, output);
        }

        private void DoHandlerTasks(Stream input, Stream output)
        {
            CancellationSource.Cancel();
            HandlerWasCalled = true;

            if (input == null)
            {
                LastInputReceived = null;
            }
            else
            {
                input.Position = 0;
                LastInputReceived = new byte[input.Length];
                input.Read(LastInputReceived);
            }

            if (output == null)
            {
                LastOutputSent = null;
            }
            else
            {
                LastOutputSent = new byte[output.Length];
                output.Read(LastOutputSent);
                output.Position = 0;
            }
        }
    }
}
