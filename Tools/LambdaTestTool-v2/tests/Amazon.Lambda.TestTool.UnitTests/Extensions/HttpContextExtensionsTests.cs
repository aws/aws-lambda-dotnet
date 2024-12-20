// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using static Amazon.Lambda.TestTool.UnitTests.Extensions.HttpContextTestCases;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions
{
    public class HttpContextExtensionsTests
    {
        [Theory]
        [MemberData(nameof(HttpContextTestCases.V1TestCases), MemberType = typeof(HttpContextTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task ToApiGatewayRequestRest_ConvertsCorrectly(string testName, HttpContextTestCase testCase)
        {
            // Arrange
            var context = testCase.HttpContext;

            // Act
            var result = await context.ToApiGatewayRequest(testCase.ApiGatewayRouteConfig, ApiGatewayEmulatorMode.Rest);

            // Assert
            testCase.Assertions(result, ApiGatewayEmulatorMode.Rest);
        }

        [Theory]
        [MemberData(nameof(HttpContextTestCases.V1TestCases), MemberType = typeof(HttpContextTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task ToApiGatewayRequestV1_ConvertsCorrectly(string testName, HttpContextTestCase testCase)
        {
            // Arrange
            var context = testCase.HttpContext;

            // Act
            var result = await context.ToApiGatewayRequest(testCase.ApiGatewayRouteConfig, ApiGatewayEmulatorMode.HttpV1);

            // Assert
            testCase.Assertions(result, ApiGatewayEmulatorMode.HttpV1);
        }

        [Theory]
        [MemberData(nameof(HttpContextTestCases.V2TestCases), MemberType = typeof(HttpContextTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task ToApiGatewayHttpV2Request_ConvertsCorrectly(string testName, HttpContextTestCase testCase)
        {
            // Arrange
            var context = testCase.HttpContext;

            // Act
            var result = await context.ToApiGatewayHttpV2Request(testCase.ApiGatewayRouteConfig);

            // Assert
            testCase.Assertions(result, ApiGatewayEmulatorMode.HttpV2);
        }

        [Fact]
        public async Task ToApiGatewayHttpV2Request_EmptyCollections()
        {

            var httpContext = CreateHttpContext("POST", "/test10/api/notmatchingpath/123/orders");
            var config = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "TestLambdaFunction",
                Endpoint = "/test10/api/users/{userId}/orders",
                HttpMethod = "POST",
                Path = "/test10/api/users/{userId}/orders"
            };

            // Act
            var result = await httpContext.ToApiGatewayHttpV2Request(config);
            Assert.Equal(2, result.Headers.Count);
            Assert.Equal("0", result.Headers["content-length"]);
            Assert.Equal("text/plain; charset=utf-8", result.Headers["content-type"]);
            Assert.Null(result.QueryStringParameters);
            Assert.Null(result.PathParameters);
            Assert.Null(result.Cookies);
        }

        [Fact]
        public async Task ToApiGatewayHttpV1Request_EmptyCollections()
        {
            var httpContext = CreateHttpContext("POST", "/test10/api/notmatchingpath/123/orders");
            var config = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "TestLambdaFunction",
                Endpoint = "/test10/api/users/{userId}/orders",
                HttpMethod = "POST",
                Path = "/test10/api/users/{userId}/orders"
            };

            // Act
            var result = await httpContext.ToApiGatewayRequest(config, ApiGatewayEmulatorMode.HttpV1);
            Assert.Equal(2, result.Headers.Count);
            Assert.Equal("0", result.Headers["content-length"]);
            Assert.Equal("text/plain; charset=utf-8", result.Headers["content-type"]);
            Assert.Equal(new List<string> { "0" }, result.MultiValueHeaders["content-length"]);
            Assert.Equal(new List<string> { "text/plain; charset=utf-8" }, result.MultiValueHeaders["content-type"]);
            Assert.Null(result.QueryStringParameters);
            Assert.Null(result.MultiValueQueryStringParameters);
            Assert.Null(result.PathParameters);
        }

        [Theory]
        [InlineData(ApiGatewayEmulatorMode.Rest)]
        [InlineData(ApiGatewayEmulatorMode.HttpV1)]
        public async Task ToApiGateway_MultiValueHeader(ApiGatewayEmulatorMode emulatorMode)
        {
            var httpContext = CreateHttpContext("POST", "/test1/api/users/123/orders",
                new Dictionary<string, StringValues>
                {
                    { "Accept", new StringValues(new[] { "text/html", "application/json" }) },
                });
            var config = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "TestLambdaFunction",
                Endpoint = "/test1/api/users/{userId}/orders",
                HttpMethod = "POST",
                Path = "/test1/api/users/{userId}/orders"
            };

            var result = await httpContext.ToApiGatewayRequest(config, emulatorMode);
            Assert.Equal(["text/html", "application/json"], result.MultiValueHeaders["accept"]);
        }

        
        [Fact]
        public async Task ToApiGatewayHttpV1_EncodedAndUnicodeHeader()
        {
            var httpContext = CreateHttpContext("POST", "/test1/api/users/123/orders",
               new Dictionary<string, StringValues>
                        {
                            { "X-Encoded-Header", "value%20with%20spaces" },
                            { "X-Unicode-Header", "☕ Coffee" },
                            { "X-Mixed-Header", "Hello%2C%20World%21%20☕" },
                            { "X-Raw-Unicode", "\u2615 Coffee" }
                        });
            var config = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "TestLambdaFunction",
                Endpoint = "/test1/api/users/{userId}/orders",
                HttpMethod = "POST",
                Path = "/test1/api/users/{userId}/orders"
            };

            var result = await httpContext.ToApiGatewayRequest(config, ApiGatewayEmulatorMode.HttpV1);
            Assert.Equal("value%20with%20spaces", result.Headers["X-Encoded-Header"]);
            Assert.Equal("☕ Coffee", result.Headers["X-Unicode-Header"]);
            Assert.Equal("Hello%2C%20World%21%20☕", result.Headers["X-Mixed-Header"]);
            Assert.Equal("\u2615 Coffee", result.Headers["X-Raw-Unicode"]);
            Assert.Equal(new List<string> { "value%20with%20spaces" }, result.MultiValueHeaders["X-Encoded-Header"]);
            Assert.Equal(new List<string> { "☕ Coffee" }, result.MultiValueHeaders["X-Unicode-Header"]);
            Assert.Equal(new List<string> { "Hello%2C%20World%21%20☕" }, result.MultiValueHeaders["X-Mixed-Header"]);
            Assert.Equal(new List<string> { "\u2615 Coffee" }, result.MultiValueHeaders["X-Raw-Unicode"]);
        }


        // Keeping this commented out for now. We have a backlog item DOTNET-7862 for this
        //[Fact]
        //public void ToApiGatewayRest_EncodedAndUnicodeHeader()
        //{
        //    var httpContext = CreateHttpContext("POST", "/test1/api/users/123/orders",
        //       new Dictionary<string, StringValues>
        //                {
        //                    { "X-Encoded-Header", "value%20with%20spaces" },
        //                    { "X-Unicode-Header", "☕ Coffee" },
        //                    { "X-Mixed-Header", "Hello%2C%20World%21%20☕" },
        //                    { "X-Raw-Unicode", "\u2615 Coffee" }
        //                });
        //    var config = new ApiGatewayRouteConfig
        //    {
        //        LambdaResourceName = "TestLambdaFunction",
        //        Endpoint = "/test1/api/users/{userId}/orders",
        //        HttpMethod = "POST",
        //        Path = "/test1/api/users/{userId}/orders"
        //    };

        //    var result = httpContext.ToApiGatewayRequest(config, ApiGatewayEmulatorMode.Rest);
        //    Assert.Equal("value%20with%20spaces", result.Headers["X-Encoded-Header"]);
        //    Assert.Equal("￢ﾘﾕ Coffee", result.Headers["X-Unicode-Header"]);
        //    Assert.Equal("Hello%2C%20World%21%20￢ﾘﾕ", result.Headers["X-Mixed-Header"]);
        //    Assert.Equal("\u2615 Coffee", result.Headers["X-Raw-Unicode"]);
        //    Assert.Equal(new List<string> { "value%20with%20spaces" }, result.MultiValueHeaders["X-Encoded-Header"]);
        //    Assert.Equal(new List<string> { "￢ﾘﾕ Coffee" }, result.MultiValueHeaders["X-Unicode-Header"]); // in reality this is what rest api thinks it is
        //    Assert.Equal(new List<string> { "Hello%2C%20World%21%20☕" }, result.MultiValueHeaders["X-Mixed-Header"]);
        //    Assert.Equal(new List<string> { "\u2615 Coffee" }, result.MultiValueHeaders["X-Raw-Unicode"]);
        //}

        [Fact]
        public async Task ToApiGateway_EncodedAndUnicodeHeaderV2()
        {
            var httpContext = CreateHttpContext("POST", "/test1/api/users/123/orders",
               new Dictionary<string, StringValues>
                        {
                            { "X-Encoded-Header", "value%20with%20spaces" },
                            { "X-Unicode-Header", "☕ Coffee" },
                            { "X-Mixed-Header", "Hello%2C%20World%21%20☕" },
                            { "X-Raw-Unicode", "\u2615 Coffee" }
                        });
            var config = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "TestLambdaFunction",
                Endpoint = "/test1/api/users/{userId}/orders",
                HttpMethod = "POST",
                Path = "/test1/api/users/{userId}/orders"
            };

            var result = await httpContext.ToApiGatewayHttpV2Request(config);
            Assert.Equal("value%20with%20spaces", result.Headers["x-encoded-header"]);
            Assert.Equal("☕ Coffee", result.Headers["x-unicode-header"]);
            Assert.Equal("Hello%2C%20World%21%20☕", result.Headers["x-mixed-header"]);
            Assert.Equal("\u2615 Coffee", result.Headers["x-raw-unicode"]);
        }

        [Theory]
        [InlineData(ApiGatewayEmulatorMode.Rest)]
        [InlineData(ApiGatewayEmulatorMode.HttpV1)]
        public async Task BinaryContentHttpV1(ApiGatewayEmulatorMode emulatorMode)
        {
            // Arrange
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

            // Act
            var result = await httpContext.ToApiGatewayRequest(config, emulatorMode);

            // Assert
            Assert.True(result.IsBase64Encoded);
            Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }), result.Body);
            Assert.Equal("123", result.PathParameters["userId"]);
            Assert.Equal("/test3/api/users/{userId}/avatar", result.Resource);
            Assert.Equal("POST", result.HttpMethod);
            Assert.Equal("application/octet-stream", result.Headers["Content-Type"]);
        }
    }
}
