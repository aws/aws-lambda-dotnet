// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using System.Text;
using Xunit;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    [Collection("ApiGateway Integration Tests")]
    public class ApiGatewayResponseExtensionsAdditionalTests
    {
        private readonly ApiGatewayIntegrationTestFixture _fixture;
        private readonly HttpClient _httpClient;

        public ApiGatewayResponseExtensionsAdditionalTests(ApiGatewayIntegrationTestFixture fixture)
        {
            _fixture = fixture;
            _httpClient = new HttpClient();
        }

        private string GetUniqueRoutePath() => $"/test-{Guid.NewGuid():N}";

        [Fact]
        public async Task ToHttpResponse_RestAPIGatewayV1DecodesBase64()
        {
            var uniqueRoute = GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToRestApi(
                _fixture.BaseRestApiId,
                _fixture.ReturnDecodedParseBinLambdaFunctionArn,
                uniqueRoute,
                binaryMediaTypes: new[] { "*/*" });

            try
            {
                var testResponse = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
                    IsBase64Encoded = true
                };

                var httpContext = new DefaultHttpContext();
                httpContext.Response.Body = new MemoryStream();
                await testResponse.ToHttpResponseAsync(httpContext, ApiGatewayEmulatorMode.Rest);
                var actualResponse = await _httpClient.PostAsync(
                    _fixture.BaseRestApiUrl + uniqueRoute, 
                    new StringContent(JsonSerializer.Serialize(testResponse)));
                
                await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpContext.Response);
                Assert.Equal(200, (int)actualResponse.StatusCode);
                var content = await actualResponse.Content.ReadAsStringAsync();
                Assert.Equal("test", content);
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromRestApi(_fixture.BaseRestApiId, routeId);
            }
        }

        [Fact]
        public async Task ToHttpResponse_HttpV1APIGatewayV1DecodesBase64()
        {
            var uniqueRoute = GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToHttpApi(
                _fixture.BaseHttpApiV1Id,
                _fixture.ParseAndReturnBodyLambdaFunctionArn,
                "1.0",
                uniqueRoute,
                "POST");

            try
            {
                var testResponse = new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
                    IsBase64Encoded = true
                };

                var httpContext = new DefaultHttpContext();
                httpContext.Response.Body = new MemoryStream();
                await testResponse.ToHttpResponseAsync(httpContext, ApiGatewayEmulatorMode.HttpV1);
                var actualResponse = await _httpClient.PostAsync(
                    _fixture.BaseHttpApiV1Url + uniqueRoute, 
                    new StringContent(JsonSerializer.Serialize(testResponse)));

                await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpContext.Response);
                Assert.Equal(200, (int)actualResponse.StatusCode);
                var content = await actualResponse.Content.ReadAsStringAsync();
                Assert.Equal("test", content);
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromHttpApi(_fixture.BaseHttpApiV1Id, routeId);
            }
        }
    }
}
