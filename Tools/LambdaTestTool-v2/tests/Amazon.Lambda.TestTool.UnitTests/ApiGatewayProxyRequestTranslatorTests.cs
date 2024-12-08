using Xunit;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Amazon.Lambda.APIGatewayEvents;
using System.Web;
using System.IO;
using System.Text;

namespace Amazon.Lambda.TestTool.UnitTests
{
    public class ApiGatewayProxyRequestTranslatorTests
    {
        private readonly ApiGatewayProxyRequestTranslator _translator;
        private readonly IHttpRequestUtility _httpRequestUtility;

        public ApiGatewayProxyRequestTranslatorTests()
        {
            _httpRequestUtility = new HttpRequestUtility();
            _translator = new ApiGatewayProxyRequestTranslator(_httpRequestUtility);
        }

        [Fact]
        public void TranslateFromHttpRequest_ShouldReturnValidApiGatewayProxyRequest()
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

            var pathParameters = new Dictionary<string, string> { { "userId", "123" } };
            var resource = "/api/users/{userId}/orders";

            var result = _translator.TranslateFromHttpRequest(request, pathParameters, resource) as APIGatewayProxyRequest;

            Assert.NotNull(result);
            Assert.Equal(resource, result.Resource);
            Assert.Equal(HttpUtility.UrlEncode("/api/users/123/orders"), result.Path);
            Assert.Equal("GET", result.HttpMethod);

            // Check single-value headers
            Assert.Equal("TestAgent", result.Headers["User-Agent"]);
            Assert.Equal("application/json", result.Headers["Accept"]);
            Assert.Equal("session=abc123; theme=dark", result.Headers["Cookie"]);
            Assert.Equal("value1", result.Headers["X-Custom-Header"]);

            // Check multi-value headers
            Assert.Equal(new List<string> { "TestAgent" }, result.MultiValueHeaders["User-Agent"]);
            Assert.Equal(new List<string> { "text/html", "application/json" }, result.MultiValueHeaders["Accept"]);
            Assert.Equal(new List<string> { "session=abc123; theme=dark" }, result.MultiValueHeaders["Cookie"]);
            Assert.Equal(new List<string> { "value1" }, result.MultiValueHeaders["X-Custom-Header"]);

            // Check query string parameters
            Assert.Equal("pending", result.QueryStringParameters["status"]);
            Assert.Equal("urgent", result.QueryStringParameters["tag"]);

            // Check multi-value query string parameters
            Assert.Equal(new List<string> { "pending" }, result.MultiValueQueryStringParameters["status"]);
            Assert.Equal(new List<string> { "important", "urgent" }, result.MultiValueQueryStringParameters["tag"]);

            Assert.Equal("123", result.PathParameters["userId"]);
            Assert.Equal(string.Empty, result.Body);
            Assert.False(result.IsBase64Encoded);
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

            var result = _translator.TranslateFromHttpRequest(request, pathParameters, resource) as APIGatewayProxyRequest;

            Assert.NotNull(result);
            Assert.True(result.IsBase64Encoded);
            Assert.Equal(Convert.ToBase64String(bodyContent), result.Body);
            Assert.Equal("123", result.PathParameters["userId"]);
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

            var result = _translator.TranslateFromHttpRequest(request, pathParameters, resource) as APIGatewayProxyRequest;

            Assert.NotNull(result);
            Assert.Empty(result.PathParameters);
            Assert.Equal(resource, result.Resource);
        }
    }
}
