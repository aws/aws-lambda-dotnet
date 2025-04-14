// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Amazon.Lambda.TestTool.Tests.Common
{
    public static class HttpContextTestCases
    {
        public static IEnumerable<object[]> V1TestCases()
        {
            yield return new object[]
            {
                "V1_SimpleGetRequest",
                new HttpContextTestCase
                {
                    HttpContext = CreateHttpContext("POST", "/test1/api/users/123/orders",
                        new Dictionary<string, StringValues>
                        {
                            { "User-Agent", "TestAgent" },
                            { "Cookie", "session=abc123; theme=dark" },
                            { "X-Custom-Header", "value1" }
                        },
                        queryString: "?status=pending&tag=important&tag=urgent"),
                    ApiGatewayRouteConfig = new ApiGatewayRouteConfig
                    {
                        LambdaResourceName = "TestLambdaFunction",
                        Endpoint = "/test1/api/users/{userId}/orders",
                        HttpMethod = "POST",
                        Path = "/test1/api/users/{userId}/orders"
                    },
                    Assertions = (result, emulatorMode) =>
                    {
                        var v1Result = Assert.IsType<APIGatewayProxyRequest>(result);
                        Assert.Equal("/test1/api/users/{userId}/orders", v1Result.Resource);
                        Assert.Equal("/test1/api/users/123/orders", v1Result.Path);
                        Assert.Equal("POST", v1Result.HttpMethod);
                        Assert.Equal("TestAgent", v1Result.Headers["User-Agent"]);
                        Assert.Equal("session=abc123; theme=dark", v1Result.Headers["Cookie"]);
                        Assert.Equal("value1", v1Result.Headers["X-Custom-Header"]);
                        Assert.Equal(new List<string> { "TestAgent" }, v1Result.MultiValueHeaders["User-Agent"]);
                        Assert.Equal(new List<string> { "session=abc123; theme=dark" }, v1Result.MultiValueHeaders["Cookie"]);
                        Assert.Equal(new List<string> { "value1" }, v1Result.MultiValueHeaders["X-Custom-Header"]);
                        Assert.Equal("pending", v1Result.QueryStringParameters["status"]);
                        Assert.Equal("urgent", v1Result.QueryStringParameters["tag"]);
                        Assert.Equal(new List<string> { "pending" }, v1Result.MultiValueQueryStringParameters["status"]);
                        Assert.Equal(new List<string> { "important", "urgent" }, v1Result.MultiValueQueryStringParameters["tag"]);
                        Assert.Equal("123", v1Result.PathParameters["userId"]);
                        Assert.Null(v1Result.Body);
                        Assert.False(v1Result.IsBase64Encoded);
                    }
                }
            };

            yield return new object[]
            {
                "V1_UrlEncodedQueryString",
                new HttpContextTestCase
                {
                    HttpContext = CreateHttpContext("POST", "/test4/api/search",
                        queryString: "?q=Hello%20World&tag=C%23%20Programming&tag=.NET%20Core"),
                    ApiGatewayRouteConfig = new ApiGatewayRouteConfig
                    {
                        LambdaResourceName = "SearchFunction",
                        Endpoint = "/test4/api/search",
                        HttpMethod = "POST",
                        Path = "/test4/api/search"
                    },
                    Assertions = (result, emulatorMode) =>
                    {
                        var v1Result = Assert.IsType<APIGatewayProxyRequest>(result);
                        Assert.Equal("Hello World", v1Result.QueryStringParameters["q"]);
                        Assert.Equal(".NET Core", v1Result.QueryStringParameters["tag"]);
                        Assert.Equal(new List<string> { "Hello World" }, v1Result.MultiValueQueryStringParameters["q"]);
                        Assert.Equal(new List<string> { "C# Programming", ".NET Core" }, v1Result.MultiValueQueryStringParameters["tag"]);
                    }
                }
            };

            yield return new object[]
            {
                "V1_SpecialCharactersInPath",
                new HttpContextTestCase
                {
                    HttpContext = CreateHttpContext("POST", "/test5/api/users/****%20Doe/orders/Summer%20Sale%202023"),
                    ApiGatewayRouteConfig = new ApiGatewayRouteConfig
                    {
                        LambdaResourceName = "UserOrdersFunction",
                        Endpoint = "/test5/api/users/{username}/orders/{orderName}",
                        HttpMethod = "POST",
                        Path = "/test5/api/users/{username}/orders/{orderName}"
                    },
                    Assertions = (result, emulatorMode) =>
                    {
                        var v1Result = Assert.IsType<APIGatewayProxyRequest>(result);

                        if (emulatorMode == ApiGatewayEmulatorMode.HttpV1)
                        {
                            Assert.Equal("/test5/api/users/****%20Doe/orders/Summer%20Sale%202023", v1Result.Path);
                            Assert.Equal("**** Doe", v1Result.PathParameters["username"]);
                            Assert.Equal("Summer Sale 2023", v1Result.PathParameters["orderName"]);
                        }
                        else
                        {
                            Assert.Equal("/test5/api/users/****%20Doe/orders/Summer%20Sale%202023", v1Result.Path);
                            Assert.Equal("****%20Doe", v1Result.PathParameters["username"]);
                            Assert.Equal("Summer%20Sale%202023", v1Result.PathParameters["orderName"]);
                        }
                    }
                }
            };

            yield return new object[]
            {
                "V1_UnicodeCharactersInPath",
                new HttpContextTestCase
                {
                    HttpContext = CreateHttpContext("POST", "/test6/api/products/%E2%98%95%20Coffee/reviews/%F0%9F%98%8A%20Happy"),
                    ApiGatewayRouteConfig = new ApiGatewayRouteConfig
                    {
                        LambdaResourceName = "ProductReviewsFunction",
                        Endpoint = "/test6/api/products/{productName}/reviews/{reviewTitle}",
                        HttpMethod = "POST",
                        Path = "/test6/api/products/{productName}/reviews/{reviewTitle}"
                    },
                    Assertions = (result, emulatorMode) =>
                    {
                        var v1Result = Assert.IsType<APIGatewayProxyRequest>(result);

                        if (emulatorMode == ApiGatewayEmulatorMode.HttpV1)
                        {
                            Assert.Equal("/test6/api/products/%E2%98%95%20Coffee/reviews/%F0%9F%98%8A%20Happy", v1Result.Path);
                            Assert.Equal("☕ Coffee", v1Result.PathParameters["productName"]);
                            Assert.Equal("😊 Happy", v1Result.PathParameters["reviewTitle"]);
                        }
                        else
                        {
                            Assert.Equal("/test6/api/products/%E2%98%95%20Coffee/reviews/%F0%9F%98%8A%20Happy", v1Result.Path);
                            Assert.Equal("%E2%98%95%20Coffee", v1Result.PathParameters["productName"]);
                            Assert.Equal("%F0%9F%98%8A%20Happy", v1Result.PathParameters["reviewTitle"]);
                        }
                    }
                }
            };
        }

        public static IEnumerable<object[]> V2TestCases()
        {
            yield return new object[]
            {
        "V2_SimpleGetRequest",
        new HttpContextTestCase
        {
            HttpContext = CreateHttpContext("POST", "/test9/api/users/123/orders",
                new Dictionary<string, StringValues>
                {
                    { "user-agent", "TestAgent" },
                    { "accept", new StringValues(new[] { "text/html", "application/json" }) },
                    { "cookie", "session=abc123; theme=dark" },
                    { "x-custom-Header", "value1" }
                },
                queryString: "?status=pending&tag=important&tag=urgent"),
            ApiGatewayRouteConfig = new ApiGatewayRouteConfig
            {
                LambdaResourceName = "TestLambdaFunction",
                Endpoint = "/test9/api/users/{userId}/orders",
                HttpMethod = "POST",
                Path = "/test9/api/users/{userId}/orders"
            },
            Assertions = (result, emulatorMode) =>
            {
                var v2Result = Assert.IsType<APIGatewayHttpApiV2ProxyRequest>(result);
                Assert.Equal("POST /test9/api/users/{userId}/orders", v2Result.RouteKey);
                Assert.Equal("/test9/api/users/123/orders", v2Result.RawPath);
                Assert.Equal("status=pending&tag=important&tag=urgent", v2Result.RawQueryString);
                Assert.Equal("TestAgent", v2Result.Headers["user-agent"]);
                Assert.Equal("text/html, application/json", v2Result.Headers["accept"]);
                Assert.Equal("value1", v2Result.Headers["x-custom-header"]);
                Assert.Equal("pending", v2Result.QueryStringParameters["status"]);
                Assert.Equal("important,urgent", v2Result.QueryStringParameters["tag"]);
                Assert.Equal("123", v2Result.PathParameters["userId"]);
                Assert.Equal(new[] { "session=abc123", "theme=dark" }, v2Result.Cookies);
                Assert.Equal("POST", v2Result.RequestContext.Http.Method);
                Assert.Equal("/test9/api/users/123/orders", v2Result.RequestContext.Http.Path);
                Assert.Equal("HTTP/1.1", v2Result.RequestContext.Http.Protocol);
            }
        }
            };

            yield return new object[]
            {
            "V2_BinaryContent",
            new HttpContextTestCase
            {
                HttpContext = CreateHttpContext("POST", "/test11/api/users/123/avatar",
                    new Dictionary<string, StringValues> { { "Content-Type", "application/octet-stream" } },
                    body: new byte[] { 1, 2, 3, 4, 5 }),
                ApiGatewayRouteConfig = new ApiGatewayRouteConfig
                {
                    LambdaResourceName = "UploadAvatarFunction",
                    Endpoint = "/test11/api/users/{userId}/avatar",
                    HttpMethod = "POST",
                    Path = "/test11/api/users/{userId}/avatar"
                },
                Assertions = (result, emulatorMode) =>
                {
                    var v2Result = Assert.IsType<APIGatewayHttpApiV2ProxyRequest>(result);
                    Assert.True(v2Result.IsBase64Encoded);
                    Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 }), v2Result.Body);
                    Assert.Equal("123", v2Result.PathParameters["userId"]);
                    Assert.Equal("POST /test11/api/users/{userId}/avatar", v2Result.RouteKey);
                    Assert.Equal("POST", v2Result.RequestContext.Http.Method);
                }
            }
            };

            yield return new object[]
            {
            "V2_UrlEncodedQueryString",
            new HttpContextTestCase
            {
                HttpContext = CreateHttpContext("POST", "/test12/api/search",
                    queryString: "?q=Hello%20World&tag=C%23%20Programming&tag=.NET%20Core"),
                ApiGatewayRouteConfig = new ApiGatewayRouteConfig
                {
                    LambdaResourceName = "SearchFunction",
                    Endpoint = "/test12/api/search",
                    HttpMethod = "POST",
                    Path = "/test12/api/search"
                },
                Assertions = (result, emulatorMode) =>
                {
                    var v2Result = Assert.IsType<APIGatewayHttpApiV2ProxyRequest>(result);
                    Assert.Equal("q=Hello%20World&tag=C%23%20Programming&tag=.NET%20Core", v2Result.RawQueryString);
                    Assert.Equal("Hello World", v2Result.QueryStringParameters["q"]);
                    Assert.Equal("C# Programming,.NET Core", v2Result.QueryStringParameters["tag"]);
                }
            }
            };

            yield return new object[]
            {
            "V2_SpecialCharactersInPath",
            new HttpContextTestCase
            {
                HttpContext = CreateHttpContext("POST", "/test13/api/users/****%20Doe/orders/Summer%20Sale%202023"),
                ApiGatewayRouteConfig = new ApiGatewayRouteConfig
                {
                    LambdaResourceName = "UserOrdersFunction",
                    Endpoint = "/test13/api/users/{username}/orders/{orderName}",
                    HttpMethod = "POST",
                    Path = "/test13/api/users/{username}/orders/{orderName}"
                },
                Assertions = (result, emulatorMode) =>
                {
                    var v2Result = Assert.IsType<APIGatewayHttpApiV2ProxyRequest>(result);
                    Assert.Equal("/test13/api/users/**** Doe/orders/Summer Sale 2023", v2Result.RawPath);
                    Assert.Equal("/test13/api/users/**** Doe/orders/Summer Sale 2023", v2Result.RequestContext.Http.Path);
                    Assert.Equal("**** Doe", v2Result.PathParameters["username"]);
                    Assert.Equal("Summer Sale 2023", v2Result.PathParameters["orderName"]);
                }
            }
            };

            yield return new object[]
            {
            "V2_UnicodeCharactersInPath",
            new HttpContextTestCase
            {
                HttpContext = CreateHttpContext("POST", "/test14/api/products/%E2%98%95%20Coffee/reviews/%F0%9F%98%8A%20Happy"),
                ApiGatewayRouteConfig = new ApiGatewayRouteConfig
                {
                    LambdaResourceName = "ProductReviewsFunction",
                    Endpoint = "/test14/api/products/{productName}/reviews/{reviewTitle}",
                    HttpMethod = "POST",
                    Path = "/test14/api/products/{productName}/reviews/{reviewTitle}"
                },
                Assertions = (result, emulatorMode) =>
                {
                    var v2Result = Assert.IsType<APIGatewayHttpApiV2ProxyRequest>(result);
                    Assert.Equal("/test14/api/products/☕ Coffee/reviews/😊 Happy", v2Result.RawPath);
                    Assert.Equal("/test14/api/products/☕ Coffee/reviews/😊 Happy", v2Result.RequestContext.Http.Path);
                    Assert.Equal("☕ Coffee", v2Result.PathParameters["productName"]);
                    Assert.Equal("😊 Happy", v2Result.PathParameters["reviewTitle"]);
                }
            }
            };
        }

        public static DefaultHttpContext CreateHttpContext(string method, string path,
            Dictionary<string, StringValues>? headers = null, string? queryString = null, byte[]? body = null)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var request = context.Request;
            request.Method = method;
            request.Path = path;
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers[header.Key] = new StringValues(header.Value.ToArray());
                }
            }
            if (queryString != null)
            {
                request.QueryString = new QueryString(queryString);
            }
            if (body != null)
            {
                request.Body = new MemoryStream(body);
                request.ContentLength = body.Length;
            }
            return context;
        }


        public class HttpContextTestCase
        {
            public required DefaultHttpContext HttpContext { get; set; }
            public required ApiGatewayRouteConfig ApiGatewayRouteConfig { get; set; }
            public required Action<object, ApiGatewayEmulatorMode> Assertions { get; set; }
        }
    }
}
