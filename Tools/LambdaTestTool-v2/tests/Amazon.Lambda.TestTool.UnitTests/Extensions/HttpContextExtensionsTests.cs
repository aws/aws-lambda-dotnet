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
        Assert.Equal("?status=pending&tag=important&tag=urgent", result.RawQueryString);
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
        Assert.Equal(HttpUtility.UrlEncode("/api/users/123/avatar"), result.Path);
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
        Assert.Equal(HttpUtility.UrlEncode("/api/users/123/orders"), result.Path);
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
}
