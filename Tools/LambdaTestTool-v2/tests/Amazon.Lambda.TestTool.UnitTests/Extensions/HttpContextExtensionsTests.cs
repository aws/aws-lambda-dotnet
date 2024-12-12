namespace Amazon.Lambda.TestTool.UnitTests.Extensions;

using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using System.Web;
using Xunit;

public class HttpContextExtensionsTests
{
    [Fact]
    public void ToApiGatewayHttpV2Request_ShouldReturnValidApiGatewayHttpApiV2ProxyRequest()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Scheme = "https";
        request.Host = new HostString("api.example.com");
        request.Path = "/api/users/123/orders";
        request.QueryString = new QueryString("?status=pending&tag=important&tag=urgent");
        request.Headers["User-Agent"] = "TestAgent";
        request.Headers["Accept"] = new Microsoft.Extensions.Primitives.StringValues(new[] { "text/html", "application/json" });
        request.Headers["Cookie"] = "session=abc123; theme=dark";
        request.Headers["X-Custom-Header"] = "value1";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestLambdaFunction",
            Endpoint = "GET /api/users/{userId}/orders",
            HttpMethod = "GET",
            Path = "/api/users/{userId}/orders"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("2.0", result.Version);
        Assert.Equal("GET /api/users/{userId}/orders", result.RouteKey);
        Assert.Equal("/api/users/123/orders", result.RawPath);
        Assert.Equal("status=pending&tag=important&tag=urgent", result.RawQueryString);
        Assert.Equal(2, result.Cookies.Length);
        Assert.Contains("session=abc123", result.Cookies);
        Assert.Contains("theme=dark", result.Cookies);
        Assert.Equal("123", result.PathParameters["userId"]);
        Assert.Equal("GET", result.RequestContext.Http.Method);
        Assert.Equal("/api/users/123/orders", result.RequestContext.Http.Path);
        Assert.Equal("GET /api/users/{userId}/orders", result.RequestContext.RouteKey);

        Assert.Equal("TestAgent", result.Headers["User-Agent"]);
        Assert.Equal("text/html,application/json", result.Headers["Accept"]);
        Assert.Equal("session=abc123; theme=dark", result.Headers["Cookie"]);
        Assert.Equal("value1", result.Headers["X-Custom-Header"]);

        Assert.Equal("pending", result.QueryStringParameters["status"]);
        Assert.Equal("important,urgent", result.QueryStringParameters["tag"]);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_WithEmptyCollections_ShouldNotSetParameters()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/notmatchingpath/123/orders";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestLambdaFunction",
            Endpoint = "GET /api/users/{userId}/orders",
            HttpMethod = "GET",
            Path = "/api/users/{userId}/orders"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Null(result.Headers);
        Assert.Null(result.QueryStringParameters);
        Assert.Null(result.PathParameters);
        Assert.Null(result.Cookies);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_WithBinaryContent_ShouldBase64EncodeBody()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "POST";
        request.Path = "/api/users/123/avatar";
        request.ContentType = "application/octet-stream";
        var bodyContent = new byte[] { 1, 2, 3, 4, 5 };
        request.Body = new MemoryStream(bodyContent);

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "UploadAvatarFunction",
            Endpoint = "POST /api/users/{userId}/avatar",
            HttpMethod = "POST",
            Path = "/api/users/{userId}/avatar"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.True(result.IsBase64Encoded);
        Assert.Equal(Convert.ToBase64String(bodyContent), result.Body);
        Assert.Equal("123", result.PathParameters["userId"]);
        Assert.Equal("POST /api/users/{userId}/avatar", result.RouteKey);
    }

    [Fact]
    public void ToApiGatewayRequest_WithBinaryContent_ShouldBase64EncodeBody()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "POST";
        request.Path = "/api/users/123/avatar";
        request.ContentType = "application/octet-stream";
        var bodyContent = new byte[] { 1, 2, 3, 4, 5 };
        request.Body = new MemoryStream(bodyContent);

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "UploadAvatarFunction",
            Endpoint = "POST /api/users/{userId}/avatar",
            HttpMethod = "POST",
            Path = "/api/users/{userId}/avatar"
        };

        var result = context.ToApiGatewayRequest(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.True(result.IsBase64Encoded);
        Assert.Equal(Convert.ToBase64String(bodyContent), result.Body);
        Assert.Equal("123", result.PathParameters["userId"]);
        Assert.Equal("/api/users/{userId}/avatar", result.Resource);
        Assert.Equal("POST", result.HttpMethod);
        Assert.Equal(HttpUtility.UrlDecode("/api/users/123/avatar"), result.Path);
    }

    [Fact]
    public void ToApiGatewayRequest_ShouldReturnValidApiGatewayProxyRequest()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Scheme = "https";
        request.Host = new HostString("api.example.com");
        request.Path = "/api/users/123/orders";
        request.QueryString = new QueryString("?status=pending&tag=important&tag=urgent");
        request.Headers["User-Agent"] = "TestAgent";
        request.Headers["Accept"] = new Microsoft.Extensions.Primitives.StringValues(new[] { "text/html", "application/json" });
        request.Headers["Cookie"] = "session=abc123; theme=dark";
        request.Headers["X-Custom-Header"] = "value1";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestLambdaFunction",
            Endpoint = "GET /api/users/{userId}/orders",
            HttpMethod = "GET",
            Path = "/api/users/{userId}/orders"
        };

        var result = context.ToApiGatewayRequest(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("/api/users/{userId}/orders", result.Resource);
        Assert.Equal(HttpUtility.UrlDecode("/api/users/123/orders"), result.Path);
        Assert.Equal("GET", result.HttpMethod);

        Assert.Equal("TestAgent", result.Headers["User-Agent"]);
        Assert.Equal("application/json", result.Headers["Accept"]);
        Assert.Equal("session=abc123; theme=dark", result.Headers["Cookie"]);
        Assert.Equal("value1", result.Headers["X-Custom-Header"]);

        Assert.Equal(new List<string> { "TestAgent" }, result.MultiValueHeaders["User-Agent"]);
        Assert.Equal(new List<string> { "text/html", "application/json" }, result.MultiValueHeaders["Accept"]);
        Assert.Equal(new List<string> { "session=abc123; theme=dark" }, result.MultiValueHeaders["Cookie"]);
        Assert.Equal(new List<string> { "value1" }, result.MultiValueHeaders["X-Custom-Header"]);

        Assert.Equal("pending", result.QueryStringParameters["status"]);
        Assert.Equal("urgent", result.QueryStringParameters["tag"]);

        Assert.Equal(new List<string> { "pending" }, result.MultiValueQueryStringParameters["status"]);
        Assert.Equal(new List<string> { "important", "urgent" }, result.MultiValueQueryStringParameters["tag"]);

        Assert.Equal("123", result.PathParameters["userId"]);
        Assert.Equal(string.Empty, result.Body);
        Assert.False(result.IsBase64Encoded);
    }

    [Fact]
    public void ToApiGatewayRequest_WithEmptyCollections_ShouldNotSetParameters()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/notmatchingpath/123/orders";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestLambdaFunction",
            Endpoint = "GET /api/users/{userId}/orders",
            HttpMethod = "GET",
            Path = "/api/users/{userId}/orders"
        };

        var result = context.ToApiGatewayRequest(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Null(result.Headers);
        Assert.Null(result.MultiValueHeaders);
        Assert.Null(result.QueryStringParameters);
        Assert.Null(result.MultiValueQueryStringParameters);
        Assert.Null(result.PathParameters);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_ShouldEncodeRawQueryString()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/search";
        request.QueryString = new QueryString("?q=Hello%20World&tag=C%23%20Programming");

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "SearchFunction",
            Endpoint = "GET /api/search",
            HttpMethod = "GET",
            Path = "/api/search"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("q=Hello%20World&tag=C%23%20Programming", result.RawQueryString);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_ShouldDecodeQueryStringParameters()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/search";
        request.QueryString = new QueryString("?q=Hello%20World&tag=C%23%20Programming&tag=.NET%20Core");

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "SearchFunction",
            Endpoint = "GET /api/search",
            HttpMethod = "GET",
            Path = "/api/search"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("Hello World", result.QueryStringParameters["q"]);
        Assert.Equal("C# Programming,.NET Core", result.QueryStringParameters["tag"]);
    }

    [Fact]
    public void ToApiGatewayRequest_ShouldDecodeQueryStringParameters()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/search";
        request.QueryString = new QueryString("?q=Hello%20World&tag=C%23%20Programming&tag=.NET%20Core");

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "SearchFunction",
            Endpoint = "GET /api/search",
            HttpMethod = "GET",
            Path = "/api/search"
        };

        var result = context.ToApiGatewayRequest(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("Hello World", result.QueryStringParameters["q"]);
        Assert.Equal(".NET Core", result.QueryStringParameters["tag"]);
        Assert.Equal(new List<string> { "Hello World" }, result.MultiValueQueryStringParameters["q"]);
        Assert.Equal(new List<string> { "C# Programming", ".NET Core" }, result.MultiValueQueryStringParameters["tag"]);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_ShouldDecodePathWithSpecialCharacters()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/users/John%20Doe/orders/Summer%20Sale%202023";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "UserOrdersFunction",
            Endpoint = "GET /api/users/{username}/orders/{orderName}",
            HttpMethod = "GET",
            Path = "/api/users/{username}/orders/{orderName}"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("/api/users/John Doe/orders/Summer Sale 2023", result.RawPath);
        Assert.Equal("/api/users/John Doe/orders/Summer Sale 2023", result.RequestContext.Http.Path);
        Assert.Equal("John Doe", result.PathParameters["username"]);
        Assert.Equal("Summer Sale 2023", result.PathParameters["orderName"]);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_ShouldDecodePathWithUnicodeCharacters()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/products/%E2%98%95%20Coffee/reviews/%F0%9F%98%8A%20Happy";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "ProductReviewsFunction",
            Endpoint = "GET /api/products/{productName}/reviews/{reviewTitle}",
            HttpMethod = "GET",
            Path = "/api/products/{productName}/reviews/{reviewTitle}"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("/api/products/☕ Coffee/reviews/😊 Happy", result.RawPath);
        Assert.Equal("/api/products/☕ Coffee/reviews/😊 Happy", result.RequestContext.Http.Path);
        Assert.Equal("☕ Coffee", result.PathParameters["productName"]);
        Assert.Equal("😊 Happy", result.PathParameters["reviewTitle"]);
    }

    [Fact]
    public void ToApiGatewayRequest_ShouldDecodePathWithSpecialCharacters()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/users/John%20Doe/orders/Summer%20Sale%202023";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "UserOrdersFunction",
            Endpoint = "GET /api/users/{username}/orders/{orderName}",
            HttpMethod = "GET",
            Path = "/api/users/{username}/orders/{orderName}"
        };

        var result = context.ToApiGatewayRequest(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("/api/users/John Doe/orders/Summer Sale 2023", result.Path);
        Assert.Equal("John Doe", result.PathParameters["username"]);
        Assert.Equal("Summer Sale 2023", result.PathParameters["orderName"]);
    }

    [Fact]
    public void ToApiGatewayRequest_ShouldDecodePathWithUnicodeCharacters()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/products/%E2%98%95%20Coffee/reviews/%F0%9F%98%8A%20Happy";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "ProductReviewsFunction",
            Endpoint = "GET /api/products/{productName}/reviews/{reviewTitle}",
            HttpMethod = "GET",
            Path = "/api/products/{productName}/reviews/{reviewTitle}"
        };

        var result = context.ToApiGatewayRequest(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("/api/products/☕ Coffee/reviews/😊 Happy", result.Path);
        Assert.Equal("☕ Coffee", result.PathParameters["productName"]);
        Assert.Equal("😊 Happy", result.PathParameters["reviewTitle"]);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_ShouldNotDecodeUrlEncodedAndUnicodeHeaderValues()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/test";
        request.Headers["X-Encoded-Header"] = "value%20with%20spaces";
        request.Headers["X-Unicode-Header"] = "☕ Coffee";
        request.Headers["X-Mixed-Header"] = "Hello%2C%20World%21%20☕";
        request.Headers["X-Raw-Unicode"] = "\u2615 Coffee";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestFunction",
            Endpoint = "GET /api/test",
            HttpMethod = "GET",
            Path = "/api/test"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("value%20with%20spaces", result.Headers["X-Encoded-Header"]);
        Assert.Equal("☕ Coffee", result.Headers["X-Unicode-Header"]);
        Assert.Equal("Hello%2C%20World%21%20☕", result.Headers["X-Mixed-Header"]);
        Assert.Equal("\u2615 Coffee", result.Headers["X-Raw-Unicode"]);
    }

    [Fact]
    public void ToApiGatewayRequest_ShouldNotDecodeUrlEncodedAndUnicodeHeaderValues()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/test";
        request.Headers["X-Encoded-Header"] = "value%20with%20spaces";
        request.Headers["X-Unicode-Header"] = "☕ Coffee";
        request.Headers["X-Mixed-Header"] = "Hello%2C%20World%21%20☕";
        request.Headers["X-Raw-Unicode"] = "\u2615 Coffee";

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestFunction",
            Endpoint = "GET /api/test",
            HttpMethod = "GET",
            Path = "/api/test"
        };

        var result = context.ToApiGatewayRequest(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("value%20with%20spaces", result.Headers["X-Encoded-Header"]);
        Assert.Equal("☕ Coffee", result.Headers["X-Unicode-Header"]);
        Assert.Equal("Hello%2C%20World%21%20☕", result.Headers["X-Mixed-Header"]);
        Assert.Equal("\u2615 Coffee", result.Headers["X-Raw-Unicode"]);

        Assert.Equal(new List<string> { "value%20with%20spaces" }, result.MultiValueHeaders["X-Encoded-Header"]);
        Assert.Equal(new List<string> { "☕ Coffee" }, result.MultiValueHeaders["X-Unicode-Header"]);
        Assert.Equal(new List<string> { "Hello%2C%20World%21%20☕" }, result.MultiValueHeaders["X-Mixed-Header"]);
        Assert.Equal(new List<string> { "\u2615 Coffee" }, result.MultiValueHeaders["X-Raw-Unicode"]);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_ShouldHandleMultipleHeaderValuesWithUnicode()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/test";
        request.Headers["X-Multi-Value"] = new string[] { "value1", "value2%20with%20spaces", "☕ Coffee", "value4%20☕" };

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestFunction",
            Endpoint = "GET /api/test",
            HttpMethod = "GET",
            Path = "/api/test"
        };

        var result = context.ToApiGatewayHttpV2Request(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("value1,value2%20with%20spaces,☕ Coffee,value4%20☕", result.Headers["X-Multi-Value"]);
    }

    [Fact]
    public void ToApiGatewayRequest_ShouldHandleMultipleHeaderValuesWithUnicode()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Path = "/api/test";
        request.Headers["X-Multi-Value"] = new string[] { "value1", "value2%20with%20spaces", "☕ Coffee", "value4%20☕" };

        var apiGatewayRouteConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestFunction",
            Endpoint = "GET /api/test",
            HttpMethod = "GET",
            Path = "/api/test"
        };

        var result = context.ToApiGatewayRequest(apiGatewayRouteConfig);

        Assert.NotNull(result);
        Assert.Equal("value4%20☕", result.Headers["X-Multi-Value"]); // v1 API uses the last value for single-value headers
        Assert.Equal(
            new List<string> { "value1", "value2%20with%20spaces", "☕ Coffee", "value4%20☕" },
            result.MultiValueHeaders["X-Multi-Value"]
        );
    }

}
