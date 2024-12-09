namespace Amazon.Lambda.TestTool.UnitTests;

using Microsoft.AspNetCore.Http;
using System.Web;
using Xunit;
using Moq;
using System.Text;
using Microsoft.Extensions.Primitives;
using System.Linq;

public class HttpContextExtensionsTests
{
    private readonly Mock<IRouteConfigurationParser> _mockRouteConfigParser;
    private readonly Mock<IHttpRequestUtility> _mockHttpRequestUtility;

    public HttpContextExtensionsTests()
    {
        _mockRouteConfigParser = new Mock<IRouteConfigurationParser>();
        _mockHttpRequestUtility = new Mock<IHttpRequestUtility>();

        _mockRouteConfigParser.Setup(x => x.GetRouteConfig(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string method, string path) => new ApiGatewayRouteConfig
            {
                LambdaResourceName = "TestLambdaFunction",
                Endpoint = $"{method} {path}",
                HttpMethod = method,
                Path = "/api/users/{userId}/orders"
            });
        _mockRouteConfigParser.Setup(x => x.ExtractPathParameters(It.IsAny<ApiGatewayRouteConfig>(), It.IsAny<string>()))
            .Returns(new Dictionary<string, string> { { "userId", "123" } });

        _mockHttpRequestUtility.Setup(x => x.ExtractHeaders(It.IsAny<IHeaderDictionary>()))
            .Returns((new Dictionary<string, string>(), new Dictionary<string, IList<string>>()));
        _mockHttpRequestUtility.Setup(x => x.ExtractQueryStringParameters(It.IsAny<IQueryCollection>()))
            .Returns((new Dictionary<string, string>(), new Dictionary<string, IList<string>>()));
        _mockHttpRequestUtility.Setup(x => x.ReadRequestBody(It.IsAny<HttpRequest>()))
            .Returns(string.Empty);
        _mockHttpRequestUtility.Setup(x => x.IsBinaryContent(It.IsAny<string>()))
            .Returns(false);

        HttpContextExtensions.SetRouteConfigurationParser(_mockRouteConfigParser.Object);
        HttpContextExtensions.SetHttpRequestUtility(_mockHttpRequestUtility.Object);
    }

    [Fact]
    public void ToApiGatewayHttpV2Request_ShouldReturnValidApiGatewayHttpApiV2ProxyRequest()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "GET";
        request.Scheme = "https";
        request.Host = new HostString("api.example.com");
        request.Path = "/api/users/123/orders";
        request.QueryString = new QueryString("?status=pending");
        request.Headers["User-Agent"] = "TestAgent";
        request.Headers["Accept"] = "application/json";
        request.Headers["Cookie"] = "session=abc123; theme=dark; complex=this+has+special;";

        var result = context.ToApiGatewayHttpV2Request();

        Assert.NotNull(result);
        Assert.Equal("2.0", result.Version);
        Assert.Equal("GET /api/users/{userId}/orders", result.RouteKey);
        Assert.Equal("/api/users/123/orders", result.RawPath);
        Assert.Equal("?status=pending", result.RawQueryString);
        Assert.Equal(3, result.Cookies.Length);
        Assert.Contains("session=abc123", result.Cookies);
        Assert.Contains("theme=dark", result.Cookies);
        Assert.Contains("complex=this%2bhas%2bspecial", result.Cookies);
        Assert.Equal("123", result.PathParameters["userId"]);
        Assert.Equal("GET", result.RequestContext.Http.Method);
        Assert.Equal("/api/users/123/orders", result.RequestContext.Http.Path);
        Assert.Equal("GET /api/users/{userId}/orders", result.RequestContext.RouteKey);

        _mockRouteConfigParser.Verify(x => x.GetRouteConfig("GET", "/api/users/123/orders"), Times.Once);
        _mockRouteConfigParser.Verify(x => x.ExtractPathParameters(It.IsAny<ApiGatewayRouteConfig>(), "/api/users/123/orders"), Times.Once);
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

        _mockHttpRequestUtility.Setup(x => x.IsBinaryContent(It.IsAny<string>())).Returns(true);
        _mockHttpRequestUtility.Setup(x => x.ReadRequestBody(It.IsAny<HttpRequest>())).Returns(Encoding.UTF8.GetString(bodyContent));

        _mockRouteConfigParser.Setup(x => x.GetRouteConfig("POST", "/api/users/123/avatar"))
            .Returns(new ApiGatewayRouteConfig
            {
                LambdaResourceName = "UploadAvatarFunction",
                Endpoint = "POST /api/users/{userId}/avatar",
                HttpMethod = "POST",
                Path = "/api/users/{userId}/avatar"
            });

        var result = context.ToApiGatewayHttpV2Request();

        Assert.NotNull(result);
        Assert.True(result.IsBase64Encoded);
        Assert.Equal(Convert.ToBase64String(bodyContent), result.Body);
        Assert.Equal("123", result.PathParameters["userId"]);
        Assert.Equal("POST /api/users/{userId}/avatar", result.RouteKey);

        _mockRouteConfigParser.Verify(x => x.GetRouteConfig("POST", "/api/users/123/avatar"), Times.Once);
        _mockRouteConfigParser.Verify(x => x.ExtractPathParameters(It.IsAny<ApiGatewayRouteConfig>(), "/api/users/123/avatar"), Times.Once);
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

        _mockHttpRequestUtility.Setup(x => x.IsBinaryContent(It.IsAny<string>())).Returns(true);
        _mockHttpRequestUtility.Setup(x => x.ReadRequestBody(It.IsAny<HttpRequest>())).Returns(Encoding.UTF8.GetString(bodyContent));

        _mockRouteConfigParser.Setup(x => x.GetRouteConfig("POST", "/api/users/123/avatar"))
            .Returns(new ApiGatewayRouteConfig
            {
                LambdaResourceName = "UploadAvatarFunction",
                Endpoint = "POST /api/users/{userId}/avatar",
                HttpMethod = "POST",
                Path = "/api/users/{userId}/avatar"
            });

        var result = context.ToApiGatewayRequest();

        Assert.NotNull(result);
        Assert.True(result.IsBase64Encoded);
        Assert.Equal(Convert.ToBase64String(bodyContent), result.Body);
        Assert.Equal("123", result.PathParameters["userId"]);
        Assert.Equal("/api/users/{userId}/avatar", result.Resource);
        Assert.Equal("POST", result.HttpMethod);
        Assert.Equal(HttpUtility.UrlEncode("/api/users/123/avatar"), result.Path);

        _mockRouteConfigParser.Verify(x => x.GetRouteConfig("POST", "/api/users/123/avatar"), Times.Once);
        _mockRouteConfigParser.Verify(x => x.ExtractPathParameters(It.IsAny<ApiGatewayRouteConfig>(), "/api/users/123/avatar"), Times.Once);
        _mockHttpRequestUtility.Verify(x => x.IsBinaryContent("application/octet-stream"), Times.Once);
        _mockHttpRequestUtility.Verify(x => x.ReadRequestBody(It.IsAny<HttpRequest>()), Times.Once);
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
        request.Headers["Accept"] = new StringValues(new[] { "text/html", "application/json" });
        request.Headers["Cookie"] = "session=abc123; theme=dark; complex=this+has+special;";
        request.Headers["X-Custom-Header"] = "value1";

        _mockHttpRequestUtility.Setup(x => x.ExtractHeaders(It.IsAny<IHeaderDictionary>()))
            .Returns((
                new Dictionary<string, string>
                {
                        { "User-Agent", "TestAgent" },
                        { "Accept", "application/json" },
                        { "X-Custom-Header", "value1" }
                },
                new Dictionary<string, IList<string>>
                {
                        { "User-Agent", new List<string> { "TestAgent" } },
                        { "Accept", new List<string> { "text/html", "application/json" } },
                        { "X-Custom-Header", new List<string> { "value1" } }
                }
            ));

        _mockHttpRequestUtility.Setup(x => x.ExtractQueryStringParameters(It.IsAny<IQueryCollection>()))
            .Returns((
                new Dictionary<string, string>
                {
                        { "status", "pending" },
                        { "tag", "urgent" }
                },
                new Dictionary<string, IList<string>>
                {
                        { "status", new List<string> { "pending" } },
                        { "tag", new List<string> { "important", "urgent" } }
                }
            ));

        var result = context.ToApiGatewayRequest();

        Assert.NotNull(result);
        Assert.Equal("/api/users/{userId}/orders", result.Resource);
        Assert.Equal(HttpUtility.UrlEncode("/api/users/123/orders"), result.Path);
        Assert.Equal("GET", result.HttpMethod);

        Assert.Equal("TestAgent", result.Headers["User-Agent"]);
        Assert.Equal("application/json", result.Headers["Accept"]);
        Assert.Equal("value1", result.Headers["X-Custom-Header"]);

        var expectedCookieString = $"session={HttpUtility.UrlEncode("abc123")}; theme={HttpUtility.UrlEncode("dark")}; complex={HttpUtility.UrlEncode("this+has+special")}";
        Assert.Equal(expectedCookieString, result.Headers["Cookie"]);
        Assert.Equal([expectedCookieString], result.MultiValueHeaders["Cookie"]);

        Assert.Equal("pending", result.QueryStringParameters["status"]);
        Assert.Equal("urgent", result.QueryStringParameters["tag"]);

        Assert.Equal(new List<string> { "pending" }, result.MultiValueQueryStringParameters["status"]);
        Assert.Equal(new List<string> { "important", "urgent" }, result.MultiValueQueryStringParameters["tag"]);

        Assert.Equal("123", result.PathParameters["userId"]);
        Assert.Equal(string.Empty, result.Body);
        Assert.False(result.IsBase64Encoded);

        _mockRouteConfigParser.Verify(x => x.GetRouteConfig("GET", "/api/users/123/orders"), Times.Once);
        _mockRouteConfigParser.Verify(x => x.ExtractPathParameters(It.IsAny<ApiGatewayRouteConfig>(), "/api/users/123/orders"), Times.Once);
    }
}
