using Xunit;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Amazon.Lambda.APIGatewayEvents;
using System.IO;
using System.Text;

namespace Amazon.Lambda.TestTool.UnitTests
{
    public class ApiGatewayHttpApiV2ProxyRequestTranslatorTests
    {
        private readonly ApiGatewayHttpApiV2ProxyRequestTranslator _translator;
        private readonly IHttpRequestUtility _httpRequestUtility;

        public ApiGatewayHttpApiV2ProxyRequestTranslatorTests()
        {
            _httpRequestUtility = new HttpRequestUtility();
            _translator = new ApiGatewayHttpApiV2ProxyRequestTranslator(_httpRequestUtility);
        }

        [Fact]
        public void TranslateFromHttpRequest_ShouldReturnValidApiGatewayHttpApiV2ProxyRequest()
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
            request.Headers["Cookie"] = "session=abc123; theme=dark";

            var pathParameters = new Dictionary<string, string> { { "userId", "123" } };
            var resource = "/api/users/{userId}/orders";

            var result = _translator.TranslateFromHttpRequest(request, pathParameters, resource) as APIGatewayHttpApiV2ProxyRequest;

            Assert.NotNull(result);
            Assert.Equal("2.0", result.Version);
            Assert.Equal($"GET {resource}", result.RouteKey);
            Assert.Equal("/api/users/123/orders", result.RawPath);
            Assert.Equal("?status=pending", result.RawQueryString);
            Assert.Equal(2, result.Cookies.Length);
            Assert.Contains("session=abc123", result.Cookies);
            Assert.Contains("theme=dark", result.Cookies);
            Assert.Equal("TestAgent", result.Headers["User-Agent"]);
            Assert.Equal("application/json", result.Headers["Accept"]);
            Assert.Equal("pending", result.QueryStringParameters["status"]);
            Assert.Equal("123", result.PathParameters["userId"]);
            Assert.True(string.IsNullOrEmpty(result.Body));
            Assert.False(result.IsBase64Encoded);
            Assert.Equal("GET", result.RequestContext.Http.Method);
            Assert.Equal("/api/users/123/orders", result.RequestContext.Http.Path);
            Assert.Equal($"GET {resource}", result.RequestContext.RouteKey);
        }

        [Fact]
        public void TranslateFromHttpRequest_WithBinaryContent_ShouldBase64EncodeBody()
        {
            var context = new DefaultHttpContext();
            var request = context.Request;
            request.Method = "POST";
            request.Path = "/api/users/123/avatar";
            request.ContentType = "application/octet-stream";
            var bodyContent = new byte[] { 1, 2, 3, 4, 5 };
            request.Body = new MemoryStream(bodyContent);

            var pathParameters = new Dictionary<string, string> { { "userId", "123" } };
            var resource = "/api/users/{userId}/avatar";

            var result = _translator.TranslateFromHttpRequest(request, pathParameters, resource) as APIGatewayHttpApiV2ProxyRequest;

            Assert.NotNull(result);
            Assert.True(result.IsBase64Encoded);
            Assert.Equal(Convert.ToBase64String(bodyContent), result.Body);
            Assert.Equal("123", result.PathParameters["userId"]);
            Assert.Equal($"POST {resource}", result.RouteKey);
        }
        [Fact]
        public void TranslateFromHttpRequest_WithEmptyPathParameters_ShouldReturnValidRequest()
        {
            var context = new DefaultHttpContext();
            var request = context.Request;
            request.Method = "GET";
            request.Path = "/api/health";

            var pathParameters = new Dictionary<string, string>();
            var resource = "/api/health";

            var result = _translator.TranslateFromHttpRequest(request, pathParameters, resource) as APIGatewayHttpApiV2ProxyRequest;

            Assert.NotNull(result);
            Assert.Empty(result.PathParameters);
            Assert.Equal($"GET {resource}", result.RouteKey);
        }
    }
}
