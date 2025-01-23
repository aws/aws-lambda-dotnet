// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;

namespace Amazon.Lambda.TestTool.IntegrationTests.Helpers
{
    public class ApiGatewayTestHelper
    {
        private readonly HttpClient _httpClient;

        public ApiGatewayTestHelper()
        {
            _httpClient = new HttpClient();
        }
        public async Task<(HttpResponseMessage actualResponse, HttpResponse httpTestResponse)> ExecuteTestRequest(APIGatewayProxyResponse testResponse, string apiUrl, ApiGatewayEmulatorMode emulatorMode)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            await testResponse.ToHttpResponseAsync(httpContext, emulatorMode);
            var serialized = JsonSerializer.Serialize(testResponse);
            var actualResponse = await _httpClient.PostAsync(apiUrl, new StringContent(serialized));
            return (actualResponse, httpContext.Response);
        }

        public async Task<(HttpResponseMessage actualResponse, HttpResponse httpTestResponse)> ExecuteTestRequest(APIGatewayHttpApiV2ProxyResponse testResponse, string apiUrl)
        {
            var testResponseHttpContext = new DefaultHttpContext();
            testResponseHttpContext.Response.Body = new MemoryStream();
            await testResponse.ToHttpResponseAsync(testResponseHttpContext);
            var serialized = JsonSerializer.Serialize(testResponse);
            var actualResponse = await _httpClient.PostAsync(apiUrl, new StringContent(serialized));
            return (actualResponse, testResponseHttpContext.Response);
        }

        public async Task AssertResponsesEqual(HttpResponseMessage actualResponse, HttpResponse httpTestResponse)
        {
            httpTestResponse.Body.Seek(0, SeekOrigin.Begin);
            var expectedContent = await new StreamReader(httpTestResponse.Body).ReadToEndAsync();
            var actualContent = await actualResponse.Content.ReadAsStringAsync();

            Assert.Equal(expectedContent, actualContent);

            Assert.Equal(httpTestResponse.StatusCode, (int)actualResponse.StatusCode);

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

            foreach (var header in httpTestResponse.Headers)
            {
                if (headersToIgnore.Contains(header.Key)) continue;
                Assert.True(actualResponse.Headers.TryGetValues(header.Key, out var actualValues) ||
                            actualResponse.Content.Headers.TryGetValues(header.Key, out actualValues),
                            $"Header '{header.Key}={string.Join(", ", header.Value.ToArray())}' not found in actual response");

                var sortedExpectedValues = header.Value.OrderBy(v => v).ToArray();
                var sortedActualValues = actualValues.OrderBy(v => v).ToArray();
                Assert.Equal(sortedExpectedValues, sortedActualValues);
            }

            foreach (var header in actualResponse.Headers.Concat(actualResponse.Content.Headers))
            {
                if (headersToIgnore.Contains(header.Key)) continue;

                Assert.True(httpTestResponse.Headers.ContainsKey(header.Key),
                            $"Header '{header.Key}={string.Join(", ", header.Value)}' not found in test response");

                var sortedExpectedValues = httpTestResponse.Headers[header.Key].OrderBy(v => v).ToArray();
                var sortedActualValues = header.Value.OrderBy(v => v).ToArray();
                Assert.Equal(sortedExpectedValues, sortedActualValues);
            }
        }
    }
}
