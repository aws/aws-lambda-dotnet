// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;
using static Amazon.Lambda.TestTool.Tests.Common.HttpContextTestCases;
using System.Net.Http.Headers;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    [Collection("ApiGateway Integration Tests")]
    public class HttpContextExtensionsTests
    {
        private readonly ApiGatewayIntegrationTestFixture _fixture;

        public HttpContextExtensionsTests(ApiGatewayIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [MemberData(nameof(HttpContextTestCases.V1TestCases), MemberType = typeof(HttpContextTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task IntegrationTest_APIGatewayV1_REST(string testName, HttpContextTestCase testCase)
        {
            var baseUrl = _fixture.GetAppropriateBaseUrl(TestRoutes.Ids.ReturnFullEvent, ApiGatewayEmulatorMode.Rest);
            var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.ReturnFullEvent);
            await RunApiGatewayTest<APIGatewayProxyRequest>(testCase, url, _fixture.MainRestApiId,
                async (context, config) => await context.ToApiGatewayRequest(config, ApiGatewayEmulatorMode.Rest),
                ApiGatewayEmulatorMode.Rest);
        }

        [Theory]
        [MemberData(nameof(HttpContextTestCases.V1TestCases), MemberType = typeof(HttpContextTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task IntegrationTest_APIGatewayV1_HTTP(string testName, HttpContextTestCase testCase)
        {
            var baseUrl = _fixture.GetAppropriateBaseUrl(TestRoutes.Ids.ReturnFullEvent, ApiGatewayEmulatorMode.HttpV1);
            var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.ReturnFullEvent);
            await RunApiGatewayTest<APIGatewayProxyRequest>(testCase, url, _fixture.MainHttpApiV1Id,
                async (context, config) => await context.ToApiGatewayRequest(config, ApiGatewayEmulatorMode.HttpV1),
                ApiGatewayEmulatorMode.HttpV1);
        }

        [Theory]
        [MemberData(nameof(HttpContextTestCases.V2TestCases), MemberType = typeof(HttpContextTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task IntegrationTest_APIGatewayV2(string testName, HttpContextTestCase testCase)
        {
            var baseUrl = _fixture.GetAppropriateBaseUrl(TestRoutes.Ids.ReturnFullEvent, ApiGatewayEmulatorMode.HttpV2);
            var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.ReturnFullEvent);
            await RunApiGatewayTest<APIGatewayHttpApiV2ProxyRequest>(testCase, url, _fixture.MainHttpApiV2Id,
                async (context, config) => await context.ToApiGatewayHttpV2Request(config),
                ApiGatewayEmulatorMode.HttpV2);
        }

        [Fact]
        public async Task BinaryContentHttpV1()
        {
            var httpContext = CreateHttpContext("POST", "/test3/api/users/123/avatar",
                         new Dictionary<string, StringValues> { { "Content-Type", "application/octet-stream" } },
                         body: new byte[] { 1, 2, 3, 4, 5 });

            var config = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "UploadAvatarFunction",
                Endpoint = "/test3/api/users/{userId}/avatar",
                HttpMethod = "POST",
                Path = "/test3/api/users/{userId}/avatar"
            };

            var testCase = new HttpContextTestCase
            {
                HttpContext = httpContext,
                ApiGatewayRouteConfig = config,
                Assertions = (actualRequest, emulatorMode) =>
                {
                    var typedRequest = (APIGatewayProxyRequest)actualRequest;
                    Assert.True(typedRequest.IsBase64Encoded);
                    Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }), typedRequest.Body);
                    Assert.Equal("123", typedRequest.PathParameters["userId"]);
                    Assert.Equal("/test3/api/users/{userId}/avatar", typedRequest.Resource);
                    Assert.Equal("POST", typedRequest.HttpMethod);
                }
            };

            var baseUrl = _fixture.GetAppropriateBaseUrl(TestRoutes.Ids.BinaryMediaType, ApiGatewayEmulatorMode.HttpV1);
            var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.BinaryMediaType);
            await RunApiGatewayTest<APIGatewayProxyRequest>(
                testCase,
                url,
                _fixture.MainHttpApiV1Id,
                async (context, cfg) => await context.ToApiGatewayRequest(cfg, ApiGatewayEmulatorMode.HttpV1),
                ApiGatewayEmulatorMode.HttpV1
            );
        }

        [Fact]
        public async Task BinaryContentRest()
        {
            var httpContext = CreateHttpContext("POST", "/test4/api/users/123/avatar",
                         new Dictionary<string, StringValues> { { "Content-Type", "application/octet-stream" } },
                         body: new byte[] { 1, 2, 3, 4, 5 });

            var config = new ApiGatewayRouteConfig
            {
                Path = "/test4/api/users/{userId}/avatar",  // Template path with parameter
                Endpoint = "/test4/api/users/{userId}/avatar",  // Same as path for this case
                HttpMethod = "POST",
                LambdaResourceName = "ReturnFullEventLambdaFunction"  // This maps to the function ARN
            };

            var testCase = new HttpContextTestCase
            {
                HttpContext = httpContext,
                ApiGatewayRouteConfig = config,
                Assertions = (actualRequest, emulatorMode) =>
                {
                    var typedRequest = (APIGatewayProxyRequest)actualRequest;
                    Assert.True(typedRequest.IsBase64Encoded);
                    Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }), typedRequest.Body);
                    Assert.Equal("123", typedRequest.PathParameters["userId"]);
                    Assert.Equal("/test4/api/users/{userId}/avatar", typedRequest.Resource);
                    Assert.Equal("POST", typedRequest.HttpMethod);
                }
            };

            // Create the route for this specific test
            var baseUrl = _fixture.GetAppropriateBaseUrl(TestRoutes.Ids.BinaryMediaType, ApiGatewayEmulatorMode.Rest);
            await _fixture.ApiGatewayHelper.AddRouteToRestApi(
                _fixture.BinaryMediaTypeRestApiId,
                _fixture.ReturnFullEventLambdaFunctionArn,  // Use the ARN from the fixture
                config.Path,
                config.HttpMethod
            );

            // Run the test
            await RunApiGatewayTest<APIGatewayProxyRequest>(
                testCase,
                baseUrl,
                _fixture.BinaryMediaTypeRestApiId,
                async (context, cfg) => await context.ToApiGatewayRequest(cfg, ApiGatewayEmulatorMode.Rest),
                ApiGatewayEmulatorMode.Rest
            );
        }

        private async Task RunApiGatewayTest<T>(HttpContextTestCase testCase, string baseUrl, string apiId, 
            Func<HttpContext, ApiGatewayRouteConfig, Task<T>> toApiGatewayRequest, ApiGatewayEmulatorMode emulatorMode)
            where T : class
        {
            var httpClient = new HttpClient();
            var stageName = emulatorMode == ApiGatewayEmulatorMode.Rest ? "/test" : "";
            var actualPath = ResolveActualPath(testCase.ApiGatewayRouteConfig.Path, testCase.HttpContext.Request.Path.Value ?? "");
            var fullUrl = baseUrl.TrimEnd('/') + stageName + actualPath + testCase.HttpContext.Request.QueryString.Value;
            
            // Wait for the API to be available
            await _fixture.ApiGatewayHelper.WaitForApiAvailability(apiId, fullUrl, emulatorMode != ApiGatewayEmulatorMode.Rest);

            // Create and send the HTTP request
            var httpRequest = new HttpRequestMessage(new HttpMethod(testCase.HttpContext.Request.Method), fullUrl);
            if (testCase.HttpContext.Request.Body != null)
            {
                var ms = new MemoryStream();
                await testCase.HttpContext.Request.Body.CopyToAsync(ms);
                httpRequest.Content = new ByteArrayContent(ms.ToArray());
                
                // Copy headers
                if (testCase.HttpContext.Request.Headers.TryGetValue("Content-Type", out var contentType) && 
                    !string.IsNullOrEmpty(contentType))
                {
                    httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                }
            }

            // Send request and get response
            var response = await httpClient.SendAsync(httpRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Verify response
            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());

            // Execute the API Gateway request transformation for assertions
            var apiGatewayRequest = await toApiGatewayRequest(testCase.HttpContext, testCase.ApiGatewayRouteConfig);
            testCase.Assertions(apiGatewayRequest, emulatorMode);
        }

        private string ResolveActualPath(string templatePath, string requestPath)
        {
            // If the template path has parameters (e.g., {userId}), use the actual request path
            if (templatePath.Contains("{"))
            {
                return requestPath;
            }
            return templatePath;
        }

        private void CompareApiGatewayRequests<T>(T expected, T actual) where T : class?
        {
            if (expected is APIGatewayProxyRequest v1Expected && actual is APIGatewayProxyRequest v1Actual)
            {
                CompareApiGatewayV1Requests(v1Expected, v1Actual);
            }
            else if (expected is APIGatewayHttpApiV2ProxyRequest v2Expected && actual is APIGatewayHttpApiV2ProxyRequest v2Actual)
            {
                CompareApiGatewayV2Requests(v2Expected, v2Actual);
            }
            else
            {
                throw new ArgumentException("Unsupported type for comparison");
            }
        }

        private void CompareApiGatewayV1Requests(APIGatewayProxyRequest expected, APIGatewayProxyRequest actual)
        {
            Assert.Equal(expected.HttpMethod, actual.HttpMethod);
            Assert.Equal(expected.Path, actual.Path);
            Assert.Equal(expected.Resource, actual.Resource);
            Assert.Equal(expected.Body, actual.Body);
            Assert.Equal(expected.IsBase64Encoded, actual.IsBase64Encoded);

            CompareHeaders(expected.Headers, actual.Headers);
            CompareMultiValueHeaders(expected.MultiValueHeaders, actual.MultiValueHeaders);
            CompareDictionaries(expected.QueryStringParameters, actual.QueryStringParameters);
            CompareDictionaries(expected.PathParameters, actual.PathParameters);
            CompareDictionaries(expected.StageVariables, actual.StageVariables);
            CompareDictionaries(expected.MultiValueQueryStringParameters, actual.MultiValueQueryStringParameters);
        }

        private void CompareApiGatewayV2Requests(APIGatewayHttpApiV2ProxyRequest expected, APIGatewayHttpApiV2ProxyRequest actual)
        {
            Assert.Equal(expected.RouteKey, actual.RouteKey);
            Assert.Equal(expected.RawPath, actual.RawPath);
            Assert.Equal(expected.RawQueryString, actual.RawQueryString);
            Assert.Equal(expected.Body, actual.Body);
            Assert.Equal(expected.IsBase64Encoded, actual.IsBase64Encoded);
            Assert.Equal(expected.Version, actual.Version);

            CompareHeaders(expected.Headers, actual.Headers);
            CompareDictionaries(expected.QueryStringParameters, actual.QueryStringParameters);
            CompareDictionaries(expected.PathParameters, actual.PathParameters);
            CompareStringArrays(expected.Cookies, actual.Cookies);

            CompareRequestContexts(expected.RequestContext, actual.RequestContext);
        }

        private void CompareHeaders(IDictionary<string, string> expected, IDictionary<string, string> actual)
        {
            var expectedFiltered = FilterHeaders(expected);
            var actualFiltered = FilterHeaders(actual);

            Assert.Equal(expectedFiltered.Count, actualFiltered.Count);

            foreach (var kvp in expectedFiltered)
            {
                Assert.True(actualFiltered.Keys.Any(k => string.Equals(k, kvp.Key, StringComparison.OrdinalIgnoreCase)),
                    $"Actual headers do not contain key: {kvp.Key}");

                var actualValue = actualFiltered.First(pair => string.Equals(pair.Key, kvp.Key, StringComparison.OrdinalIgnoreCase)).Value;
                Assert.Equal(kvp.Value, actualValue);
            }
        }

        private void CompareMultiValueHeaders(IDictionary<string, IList<string>> expected, IDictionary<string, IList<string>> actual)
        {
            var expectedFiltered = FilterHeaders(expected);
            var actualFiltered = FilterHeaders(actual);

            Assert.Equal(expectedFiltered.Count, actualFiltered.Count);

            foreach (var kvp in expectedFiltered)
            {
                Assert.True(actualFiltered.Keys.Any(k => string.Equals(k, kvp.Key, StringComparison.OrdinalIgnoreCase)),
                    $"Actual headers do not contain key: {kvp.Key}");

                var actualValue = actualFiltered.First(pair => string.Equals(pair.Key, kvp.Key, StringComparison.OrdinalIgnoreCase)).Value;
                Assert.Equal(kvp.Value, actualValue);
            }
        }

        private IDictionary<TKey, TValue> FilterHeaders<TKey, TValue>(IDictionary<TKey, TValue> headers) where TKey : notnull
        {
            return headers.Where(kvp =>
                !(kvp.Key.ToString()!.StartsWith("x-forwarded-", StringComparison.OrdinalIgnoreCase) || // ignore these for now
                  kvp.Key.ToString()!.StartsWith("cloudfront-", StringComparison.OrdinalIgnoreCase) || // ignore these for now
                  kvp.Key.ToString()!.StartsWith("via-", StringComparison.OrdinalIgnoreCase) || // ignore these for now
                  kvp.Key.ToString()!.Equals("x-amzn-trace-id", StringComparison.OrdinalIgnoreCase) || // this is dynamic so ignoring for now
                  kvp.Key.ToString()!.Equals("cookie", StringComparison.OrdinalIgnoreCase) || // TODO may have to have api gateway v2 not set this in headers
                  kvp.Key.ToString()!.Equals("host", StringComparison.OrdinalIgnoreCase))) // TODO we may want to set this
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private void CompareDictionaries<TKey, TValue>(IDictionary<TKey, TValue>? expected, IDictionary<TKey, TValue>? actual)
        {
            if (expected == null && actual == null) return;
            if (expected == null && actual != null) Assert.Fail();
            if (expected != null && actual == null) Assert.Fail();
            Assert.Equal(expected!.Count, actual!.Count);

            foreach (var kvp in expected)
            {
                Assert.True(actual.ContainsKey(kvp.Key), $"Actual does not contain key: {kvp.Key}");
                Assert.Equal(kvp.Value, actual[kvp.Key]);
            }
        }

        private void CompareStringArrays(string[] expected, string[] actual)
        {
            Assert.Equal(expected?.Length, actual?.Length);
            if (expected != null)
            {
                Assert.Equal(expected.OrderBy(x => x), actual?.OrderBy(x => x));
            }
        }

        private void CompareRequestContexts(APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext expected, APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext actual)
        {
            Assert.Equal(expected.RouteKey, actual.RouteKey);

            Assert.Equal(expected.Http.Method, actual.Http.Method);
            Assert.Equal(expected.Http.Path, actual.Http.Path);
            Assert.Equal(expected.Http.Protocol, actual.Http.Protocol);
            Assert.Equal(expected.Http.UserAgent, actual.Http.UserAgent);
        }

        private HttpRequestMessage CreateHttpRequestMessage(HttpContext context, string fullUrl)
        {
            var request = context.Request;
            var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), fullUrl);

            foreach (var header in request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            if (request.ContentLength > 0)
            {
                var bodyStream = new MemoryStream();
                request.Body.CopyTo(bodyStream);
                bodyStream.Position = 0;
                httpRequest.Content = new StreamContent(bodyStream);

                // Set Content-Type if present in the original request
                if (request.ContentType != null)
                {
                    httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.ContentType);
                }
            }
            else
            {
                httpRequest.Content = new StringContent(string.Empty);
            }

            httpRequest.Version = HttpVersion.Version11;

            return httpRequest;
        }
    }
}
