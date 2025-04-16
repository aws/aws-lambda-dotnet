// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.UnitTests.SnapshotHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;
using static Amazon.Lambda.TestTool.UnitTests.Extensions.HttpContextTestCases;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions
{
    public class HttpContextExtensionsTests
    {
        private readonly SnapshotTestHelper _snapshots;

        public HttpContextExtensionsTests()
        {
            _snapshots = new SnapshotTestHelper();
        }

        [Theory]
        [MemberData(nameof(V1TestCases), MemberType = typeof(HttpContextTestCases))]
        public Task APIGatewayV1_REST(string testName, HttpContextTestCase testCase)
        {
            var testCaseName = testName + ApiGatewayEmulatorMode.Rest;
            return RunApiGatewayTest<APIGatewayProxyRequest>(testCase, ApiGatewayEmulatorMode.Rest, testCaseName);
        }

        [Theory]
        [MemberData(nameof(V1TestCases), MemberType = typeof(HttpContextTestCases))]
        public Task APIGatewayV1_HTTP(string testName, HttpContextTestCase testCase)
        {
            var testCaseName = testName + ApiGatewayEmulatorMode.HttpV1;

            return RunApiGatewayTest<APIGatewayProxyRequest>(testCase, ApiGatewayEmulatorMode.HttpV1, testCaseName);
        }

        [Theory]
        [MemberData(nameof(V2TestCases), MemberType = typeof(HttpContextTestCases))]
        public Task APIGatewayV2(string testName, HttpContextTestCase testCase)
        {
            return RunApiGatewayTest<APIGatewayHttpApiV2ProxyRequest>(testCase,  ApiGatewayEmulatorMode.HttpV2, testName);
        }

        [Fact]
        public Task BinaryContentHttpV1()
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

            return RunApiGatewayTest<APIGatewayProxyRequest>(testCase, ApiGatewayEmulatorMode.HttpV1, nameof(BinaryContentHttpV1));
        }

        [Fact]
        public Task BinaryContentRest()
        {
            var httpContext = CreateHttpContext("POST", "/test4/api/users/123/avatar",
                         new Dictionary<string, StringValues> { { "Content-Type", "application/octet-stream" } },
                         body: new byte[] { 1, 2, 3, 4, 5 });

            var config = new ApiGatewayRouteConfig
            {
                Path = "/test4/api/users/{userId}/avatar",
                Endpoint = "/test4/api/users/{userId}/avatar",
                HttpMethod = "POST",
                LambdaResourceName = "ReturnFullEventLambdaFunction"
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

            return RunApiGatewayTest<APIGatewayProxyRequest>(testCase, ApiGatewayEmulatorMode.Rest, nameof(BinaryContentRest));
        }

        private async Task RunApiGatewayTest<T>(HttpContextTestCase testCase, ApiGatewayEmulatorMode emulatorMode, string testName) where T : class
        {

            Func<HttpContext, ApiGatewayRouteConfig, Task<T>> converter = emulatorMode switch
            {
                ApiGatewayEmulatorMode.Rest =>
                    async (context, cfg) => (T)(object)await context.ToApiGatewayRequest(cfg, ApiGatewayEmulatorMode.Rest),
                ApiGatewayEmulatorMode.HttpV1 =>
                    async (context, cfg) => (T)(object)await context.ToApiGatewayRequest(cfg, ApiGatewayEmulatorMode.HttpV1),
                ApiGatewayEmulatorMode.HttpV2 =>
                    async (context, cfg) => (T)(object)await context.ToApiGatewayHttpV2Request(cfg),
                _ => throw new ArgumentException($"Unsupported gateway type: {emulatorMode}")
            };

            await RunApiGatewayTestInternal(
                    testCase,
                    converter,
                    emulatorMode,
                    testName);
        }

        private async Task RunApiGatewayTestInternal<T>(
            HttpContextTestCase testCase,
            Func<HttpContext, ApiGatewayRouteConfig, Task<T>> toApiGatewayRequest,
            ApiGatewayEmulatorMode emulatorMode,
            string testName)
            where T : class
        {
            T snapshot;
            snapshot = await _snapshots.LoadSnapshot<T>(testName);
            var expectedApiGatewayRequest = await toApiGatewayRequest(testCase.HttpContext, testCase.ApiGatewayRouteConfig);
            CompareApiGatewayRequests(expectedApiGatewayRequest, snapshot);
            testCase.Assertions(expectedApiGatewayRequest!, emulatorMode);
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
    }
}
