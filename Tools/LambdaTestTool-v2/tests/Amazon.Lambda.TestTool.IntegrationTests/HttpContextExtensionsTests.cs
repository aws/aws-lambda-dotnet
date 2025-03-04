// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Tests.Common;
using Amazon.Lambda.TestTool.IntegrationTests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;
using static Amazon.Lambda.TestTool.Tests.Common.HttpContextTestCases;

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
            var uniqueRoute = TestUtils.GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToRestApi(
                _fixture.BaseRestApiId,
                _fixture.ReturnFullEventLambdaFunctionArn,
                uniqueRoute);

            try
            {
                await RunApiGatewayTest<APIGatewayProxyRequest>(
                    testCase,
                    _fixture.BaseRestApiUrl + uniqueRoute,
                    _fixture.BaseRestApiId,
                    async (context, config) => await context.ToApiGatewayRequest(config, ApiGatewayEmulatorMode.Rest),
                    ApiGatewayEmulatorMode.Rest);
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromRestApi(_fixture.BaseRestApiId, routeId);
            }
        }

        [Theory]
        [MemberData(nameof(HttpContextTestCases.V1TestCases), MemberType = typeof(HttpContextTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task IntegrationTest_APIGatewayV1_HTTP(string testName, HttpContextTestCase testCase)
        {
            var uniqueRoute = TestUtils.GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToHttpApi(
                _fixture.BaseHttpApiV1Id,
                _fixture.ReturnFullEventLambdaFunctionArn,
                "1.0",
                uniqueRoute,
                "POST");

            try
            {
                await RunApiGatewayTest<APIGatewayProxyRequest>(
                    testCase,
                    _fixture.BaseHttpApiV1Url + uniqueRoute,
                    _fixture.BaseHttpApiV1Id,
                    async (context, config) => await context.ToApiGatewayRequest(config, ApiGatewayEmulatorMode.HttpV1),
                    ApiGatewayEmulatorMode.HttpV1);
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromHttpApi(_fixture.BaseHttpApiV1Id, routeId);
            }
        }

        [Theory]
        [MemberData(nameof(HttpContextTestCases.V2TestCases), MemberType = typeof(HttpContextTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task IntegrationTest_APIGatewayV2(string testName, HttpContextTestCase testCase)
        {
            var uniqueRoute = TestUtils.GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToHttpApi(
                _fixture.BaseHttpApiV2Id,
                _fixture.ReturnFullEventLambdaFunctionArn,
                "2.0",
                uniqueRoute,
                "POST");

            try
            {
                await RunApiGatewayTest<APIGatewayHttpApiV2ProxyRequest>(
                    testCase,
                    _fixture.BaseHttpApiV2Url + uniqueRoute,
                    _fixture.BaseHttpApiV2Id,
                    async (context, config) => await context.ToApiGatewayHttpV2Request(config),
                    ApiGatewayEmulatorMode.HttpV2);
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromHttpApi(_fixture.BaseHttpApiV2Id, routeId);
            }
        }

        [Fact]
        public async Task BinaryContentHttpV1()
        {
            var uniqueRoute = TestUtils.GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToHttpApi(
                _fixture.BaseHttpApiV1Id,
                _fixture.ReturnFullEventLambdaFunctionArn,
                "1.0",
                uniqueRoute,
                "POST");

            var httpContext = CreateHttpContext("POST", uniqueRoute,
                         new Dictionary<string, StringValues> { { "Content-Type", "application/octet-stream" } },
                         body: new byte[] { 1, 2, 3, 4, 5 });

            var config = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "UploadAvatarFunction",
                Endpoint = uniqueRoute,
                HttpMethod = "POST",
                Path = uniqueRoute
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
                    Assert.Equal(uniqueRoute, typedRequest.Resource);
                    Assert.Equal("POST", typedRequest.HttpMethod);
                }
            };

            try
            {
                await RunApiGatewayTest<APIGatewayProxyRequest>(
                    testCase,
                    _fixture.BaseHttpApiV1Url + uniqueRoute,
                    _fixture.BaseHttpApiV1Id,
                    async (context, cfg) => await context.ToApiGatewayRequest(cfg, ApiGatewayEmulatorMode.HttpV1),
                    ApiGatewayEmulatorMode.HttpV1
                );
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromHttpApi(_fixture.BaseHttpApiV1Id, routeId);
            }
        }

        [Fact]
        public async Task BinaryContentRest()
        {
            var uniqueRoute = TestUtils.GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToRestApi(
                _fixture.BaseRestApiId,
                _fixture.ReturnFullEventLambdaFunctionArn,
                uniqueRoute,
                binaryMediaTypes: new[] { "*/*" });

            var httpContext = CreateHttpContext("POST", uniqueRoute,
                         new Dictionary<string, StringValues> { { "Content-Type", "application/octet-stream" } },
                         body: new byte[] { 1, 2, 3, 4, 5 });

            var config = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "UploadAvatarFunction",
                Endpoint = uniqueRoute,
                HttpMethod = "POST",
                Path = uniqueRoute
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
                    Assert.Equal(uniqueRoute, typedRequest.Resource);
                    Assert.Equal("POST", typedRequest.HttpMethod);
                }
            };

            try
            {
                await RunApiGatewayTest<APIGatewayProxyRequest>(
                    testCase,
                    _fixture.BaseRestApiUrl + uniqueRoute,
                    _fixture.BaseRestApiId,
                    async (context, cfg) => await context.ToApiGatewayRequest(cfg, ApiGatewayEmulatorMode.Rest),
                    ApiGatewayEmulatorMode.Rest
                );
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromRestApi(_fixture.BaseRestApiId, routeId);
            }
        }

        private async Task RunApiGatewayTest<T>(HttpContextTestCase testCase, string apiUrl, string apiId, Func<HttpContext, ApiGatewayRouteConfig, Task<T>> toApiGatewayRequest, ApiGatewayEmulatorMode emulatorMode)
            where T : class
        {
            var httpClient = new HttpClient();

            var uri = new Uri(apiUrl);
            var baseUrl = $"{uri.Scheme}://{uri.Authority}";
            var stageName = emulatorMode == ApiGatewayEmulatorMode.Rest ? "/test" : ""; // matching hardcoded test stage name for rest api. TODO update this logic later to not be hardcoded
            var actualPath = ResolveActualPath(testCase.ApiGatewayRouteConfig.Path, testCase.HttpContext.Request.Path);
            var fullUrl = baseUrl + stageName + actualPath + testCase.HttpContext.Request.QueryString;
            await _fixture.ApiGatewayHelper.WaitForApiAvailability(apiId, fullUrl, emulatorMode != ApiGatewayEmulatorMode.Rest);

            var httpRequest = CreateHttpRequestMessage(testCase.HttpContext, fullUrl);

            var response = await httpClient.SendAsync(httpRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());

            var actualApiGatewayRequest = JsonSerializer.Deserialize<T>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var expectedApiGatewayRequest = await toApiGatewayRequest(testCase.HttpContext, testCase.ApiGatewayRouteConfig);

            CompareApiGatewayRequests(expectedApiGatewayRequest, actualApiGatewayRequest);

            testCase.Assertions(actualApiGatewayRequest!, emulatorMode);

            await Task.Delay(1000); // Small delay between requests
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

        private string ResolveActualPath(string routeWithPlaceholders, string actualPath)
        {
            var routeParts = routeWithPlaceholders.Split('/');
            var actualParts = actualPath.Split('/');

            if (routeParts.Length != actualParts.Length)
            {
                throw new ArgumentException("Route and actual path have different number of segments");
            }

            var resolvedParts = new List<string>();
            for (int i = 0; i < routeParts.Length; i++)
            {
                if (routeParts[i].StartsWith("{") && routeParts[i].EndsWith("}"))
                {
                    resolvedParts.Add(actualParts[i]);
                }
                else
                {
                    resolvedParts.Add(routeParts[i]);
                }
            }

            return string.Join("/", resolvedParts);
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
