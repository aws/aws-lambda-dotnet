// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using System.Text;
using Amazon.Lambda.TestTool.IntegrationTests.Helpers.snapshot;
using Xunit;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    [Collection("ApiGateway Integration Tests")]
    public class ApiGatewayResponseExtensionsAdditionalTests
    {
        private readonly ApiGatewayIntegrationTestFixture _fixture;
        private readonly HttpClient _httpClient;
        private readonly SnapshotTestHelper _snapshots;


        public ApiGatewayResponseExtensionsAdditionalTests(ApiGatewayIntegrationTestFixture fixture)
        {
            _fixture = fixture;
            _httpClient = new HttpClient();
            _snapshots = new SnapshotTestHelper(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new HttpResponseMessageConverter() }
                }
            );
        }

        [Fact]
        public async Task ToHttpResponse_RestAPIGatewayV1DecodesBase64()
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

            if (_snapshots.IsUpdatingSnapshots)
            {
                var baseUrl = _fixture.GetAppropriateBaseUrl(ApiGatewayType.RestWithBinarySupport);
                var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.DecodeParseBinary);
                var actualResponse = await _httpClient.PostAsync(
                    url,
                    new StringContent(JsonSerializer.Serialize(testResponse)),
                    new CancellationTokenSource(5000).Token);

                await _snapshots.SaveSnapshot(actualResponse, nameof(ToHttpResponse_RestAPIGatewayV1DecodesBase64));
            }
            else
            {
                var snapshot = await _snapshots.LoadSnapshot<HttpResponseMessage>(
                    nameof(ToHttpResponse_RestAPIGatewayV1DecodesBase64));

                Assert.Equal(200, (int)snapshot.StatusCode);
                var content = await snapshot.Content.ReadAsStringAsync();
                Assert.Equal("test", content);
            }
        }

        [Fact]
        public async Task ToHttpResponse_HttpV1APIGatewayV1DecodesBase64()
        {
            var testResponse = new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
                IsBase64Encoded = true
            };

            var baseUrl = _fixture.GetAppropriateBaseUrl(ApiGatewayType.HttpV1);
            var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.ParseAndReturnBody);

            if (_snapshots.IsUpdatingSnapshots)
            {
                var actualResponse = await _httpClient.PostAsync(
                    url,
                    new StringContent(JsonSerializer.Serialize(testResponse)),
                    new CancellationTokenSource(5000).Token);

                await _snapshots.SaveSnapshot(actualResponse, nameof(ToHttpResponse_HttpV1APIGatewayV1DecodesBase64));
            }
            else
            {
                var snapshot = await _snapshots.LoadSnapshot<HttpResponseMessage>(
                    nameof(ToHttpResponse_HttpV1APIGatewayV1DecodesBase64));

                Assert.Equal(200, (int)snapshot.StatusCode);
                var content = await snapshot.Content.ReadAsStringAsync();
                Assert.Equal("test", content);
            }
        }
    }
}
