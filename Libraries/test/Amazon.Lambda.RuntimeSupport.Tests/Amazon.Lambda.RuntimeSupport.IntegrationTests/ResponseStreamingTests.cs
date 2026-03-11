// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.IdentityManagement;
using Amazon.Lambda.Model;
using Amazon.Runtime.EventStreams;
using Amazon.S3;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests
{
    [Collection("Integration Tests")]
    public class ResponseStreamingTests : BaseCustomRuntimeTest
    {
        private readonly static string s_functionName = "IntegTestResponseStreamingFunctionHandlers" + DateTime.Now.Ticks;

        private readonly ResponseStreamingTestsFixture _streamFixture;

        public ResponseStreamingTests(IntegrationTestFixture fixture, ResponseStreamingTestsFixture streamFixture)
            : base(fixture, s_functionName, "ResponseStreamingFunctionHandlers.zip", @"ResponseStreamingFunctionHandlers\bin\Release\net10.0\ResponseStreamingFunctionHandlers.zip", "ResponseStreamingFunctionHandlers")
        { 
            _streamFixture = streamFixture;
        }

        [Fact]
        public async Task SimpleFunctionHandler()
        {
            await _streamFixture.EnsureResourcesDeployedAsync(this);

            var evnts = await InvokeFunctionAsync(nameof(SimpleFunctionHandler));
            Assert.True(evnts.Any());

            var content = GetCombinedStreamContent(evnts);
            Assert.Equal("Hello, World!", content);
        }

        [Fact]
        public async Task StreamContentHandler()
        {
            await _streamFixture.EnsureResourcesDeployedAsync(this);

            var evnts = await InvokeFunctionAsync(nameof(StreamContentHandler));
            Assert.True(evnts.Length > 5);

            var content = GetCombinedStreamContent(evnts);
            Assert.Contains("Line 9999", content);
            Assert.EndsWith("Finish stream content\n", content);
        }

        [Fact]
        public async Task UnhandledExceptionHandler()
        {
            await _streamFixture.EnsureResourcesDeployedAsync(this);

            var evnts = await InvokeFunctionAsync(nameof(UnhandledExceptionHandler));
            Assert.True(evnts.Any());

            var content = GetCombinedStreamContent(evnts);
            Assert.Contains("This method will fail", content);
            Assert.Contains("This is an unhandled exception", content);
            Assert.Contains("Lambda-Runtime-Function-Error-Type", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("This is an unhandled exception", content);
            Assert.Contains("stackTrace", content);
        }

        private async Task<IEventStreamEvent[]> InvokeFunctionAsync(string handlerScenario)
        {
            using var client = new AmazonLambdaClient(TestRegion);

            var request = new InvokeWithResponseStreamRequest
            {
                FunctionName = base.FunctionName,
                Payload = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"\"{handlerScenario}\"")),
                InvocationType = ResponseStreamingInvocationType.RequestResponse
            };

            var response = await client.InvokeWithResponseStreamAsync(request);
            var evnts = response.EventStream.AsEnumerable().ToArray();
            return evnts;
        }

        private string GetCombinedStreamContent(IEventStreamEvent[] events)
        {
            var sb = new StringBuilder();
            foreach (var evnt in events)
            {
                if (evnt is InvokeResponseStreamUpdate chunk)
                {
                    var text = System.Text.Encoding.UTF8.GetString(chunk.Payload.ToArray());
                    sb.Append(text);
                }
            }
            return sb.ToString();
        }
    }

    public class ResponseStreamingTestsFixture : IAsyncLifetime
    {
        private readonly AmazonLambdaClient _lambdaClient = new AmazonLambdaClient(BaseCustomRuntimeTest.TestRegion);
        private readonly AmazonS3Client _s3Client = new AmazonS3Client(BaseCustomRuntimeTest.TestRegion);
        private readonly AmazonIdentityManagementServiceClient _iamClient = new AmazonIdentityManagementServiceClient(BaseCustomRuntimeTest.TestRegion);
        bool _resourcesCreated;
        bool _roleAlreadyExisted;

        ResponseStreamingTests _tests;

        public async Task EnsureResourcesDeployedAsync(ResponseStreamingTests tests)
        {
            if (_resourcesCreated)
                return;

            _tests = tests;
            _roleAlreadyExisted = await _tests.PrepareTestResources(_s3Client, _lambdaClient, _iamClient);

            _resourcesCreated = true;
        }

        public async Task DisposeAsync()
        {
                await _tests.CleanUpTestResources(_s3Client, _lambdaClient, _iamClient, _roleAlreadyExisted);

                _lambdaClient.Dispose();
                _s3Client.Dispose();
                _iamClient.Dispose();
        }

        public Task InitializeAsync() => Task.CompletedTask;
    }
}
