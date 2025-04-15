// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.UnitTests.SnapshotHelper;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests
{
    public class ApiGatewayTestHelper
    {
        private readonly SnapshotTestHelper _snapshots = new(new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new HttpResponseMessageConverter() }
            }
        );

        public async Task VerifyApiGatewayResponseAsync(
            APIGatewayProxyResponse response,
            ApiGatewayEmulatorMode emulatorMode,
            string snapshotName)
        {
            // Convert response to HttpResponse (simulates what API Gateway would do)
            var convertedResponse = await ConvertToHttpResponseAsync(response, emulatorMode);

            // Load the expected response from snapshot
            var expectedResponse = await _snapshots.LoadSnapshot<HttpResponseMessage>(snapshotName);

            // Compare the responses
            await AssertResponsesEqual(expectedResponse, convertedResponse);
        }

        public async Task VerifyHttpApiV2ResponseAsync(
            APIGatewayHttpApiV2ProxyResponse response,
            string snapshotName)
        {
            // Convert response to HttpResponse (simulates what API Gateway would do)
            var convertedResponse = await ConvertToHttpResponseAsync(response);

            // Load the expected response from snapshot
            var expectedResponse = await _snapshots.LoadSnapshot<HttpResponseMessage>(snapshotName);

            // Compare the responses
            await AssertResponsesEqual(expectedResponse, convertedResponse);
        }

        private async Task<HttpResponse> ConvertToHttpResponseAsync(
            APIGatewayProxyResponse response,
            ApiGatewayEmulatorMode emulatorMode)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            await response.ToHttpResponseAsync(context, emulatorMode);
            return context.Response;
        }

        private async Task<HttpResponse> ConvertToHttpResponseAsync(
            APIGatewayHttpApiV2ProxyResponse response)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            await response.ToHttpResponseAsync(context);
            return context.Response;
        }

        private async Task AssertResponsesEqual(HttpResponseMessage expected, HttpResponse actual)
        {
            actual.Body.Seek(0, SeekOrigin.Begin);
            var actualContent = await new StreamReader(actual.Body).ReadToEndAsync();
            var expectedContent = await expected.Content.ReadAsStringAsync();

            Assert.Equal(actualContent, expectedContent);

            Assert.Equal(actual.StatusCode, (int)expected.StatusCode);

            // ignore these because they will vary in the real world. we will check manually in other test cases that these are set
            var headersToIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Date",
                "Apigw-Requestid",
                "X-Amzn-Trace-Id",
                "x-amzn-RequestId",
                "x-amz-apigw-id",
                "X-Cache",
                "Via",
                "X-Amz-Cf-Pop",
                "X-Amz-Cf-Id"
            };

            foreach (var header in actual.Headers)
            {
                if (headersToIgnore.Contains(header.Key)) continue;
                Assert.True(expected.Headers.TryGetValues(header.Key, out var expectedValues) ||
                            expected.Content.Headers.TryGetValues(header.Key, out expectedValues),
                            $"Header '{header.Key}={string.Join(", ", header.Value.ToArray())}' not found in expected response");

                var sortedActualValues = header.Value.OrderBy(v => v).ToArray();
                var sortedExpectedValues = expectedValues.OrderBy(v => v).ToArray();
                Assert.Equal(sortedActualValues, sortedExpectedValues);
            }

            foreach (var header in expected.Headers.Concat(expected.Content.Headers))
            {
                if (headersToIgnore.Contains(header.Key)) continue;

                Assert.True(actual.Headers.ContainsKey(header.Key),
                            $"Header '{header.Key}={string.Join(", ", header.Value)}' not found in actual response");

                var sortedActualValues = actual.Headers[header.Key].OrderBy(v => v).ToArray();
                var sortedExpectedValues = header.Value.OrderBy(v => v).ToArray();
                Assert.Equal(sortedActualValues, sortedExpectedValues);
            }
        }
    }
}
