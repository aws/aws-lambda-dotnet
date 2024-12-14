// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Amazon.Lambda.TestTool.Extensions;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    [Collection("ApiGateway Integration Tests")]
    public class ApiGatewayResponseExtensionsTestsManual
    {
        private readonly ApiGatewayIntegrationTestFixture _fixture;
        private readonly HttpClient _httpClient;

        public ApiGatewayResponseExtensionsTestsManual(ApiGatewayIntegrationTestFixture fixture)
        {
            _fixture = fixture;
            _httpClient = new HttpClient();
        }

        [Fact]
        public async Task V2_SetsContentTypeApplicationJsonWhenNoStatusProvided()
        {
            var testResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = "Hello from lambda"
            };

            var httpContext = new DefaultHttpContext();
            testResponse.ToHttpResponse(httpContext);
            var actualResponse = await _httpClient.PostAsync(_fixture.ReturnRawRequestBodyHttpApiV2Url, new StringContent("Hello from lambda"));

            await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpContext.Response);
            Assert.Equal(200, (int)actualResponse.StatusCode);
            Assert.Equal("application/json", actualResponse.Content.Headers.ContentType?.ToString());
            var content = await actualResponse.Content.ReadAsStringAsync();
            Assert.Equal("Hello from lambda", content);
        }

        [Fact]
        public async Task V2_SetsContentTypeApplicationJsonWhenNoStatusProvidedAndDoesntUseOtherType()
        {
            var payload = "{\"key\" : \"value\", \"headers\" : {\"content-type\": \"application/xml\"}}";

            var testResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = payload
            };

            var httpContext = new DefaultHttpContext();
            testResponse.ToHttpResponse(httpContext);

            var actualResponse = await _httpClient.PostAsync(_fixture.ReturnRawRequestBodyHttpApiV2Url, new StringContent(payload));

            await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpContext.Response);
            Assert.Equal(200, (int)actualResponse.StatusCode);
            Assert.Equal("application/json", actualResponse.Content.Headers.ContentType?.ToString());
            var content = await actualResponse.Content.ReadAsStringAsync();

            var responsePayload = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
            Assert.Equal("value", responsePayload?["key"].ToString());
        }
    }
}
