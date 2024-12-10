namespace Amazon.Lambda.TestTool.UnitTests.Extensions;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using System.IO;
using System.Text;
using Xunit;

public class ApiGatewayResponseExtensionsTests
{
    [Fact]
    public void ToHttpResponse_APIGatewayProxyResponse_SetsCorrectStatusCode()
    {
        var apiResponse = new APIGatewayProxyResponse
        {
            StatusCode = 200
        };

        var httpResponse = apiResponse.ToHttpResponse();

        Assert.Equal(200, httpResponse.StatusCode);
    }

    [Fact]
    public void ToHttpResponse_APIGatewayProxyResponse_SetsHeaders()
    {
        var apiResponse = new APIGatewayProxyResponse
        {
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "X-Custom-Header", "CustomValue" }
            }
        };

        var httpResponse = apiResponse.ToHttpResponse();

        Assert.Equal("application/json", httpResponse.Headers["Content-Type"]);
        Assert.Equal("CustomValue", httpResponse.Headers["X-Custom-Header"]);
    }

    [Fact]
    public void ToHttpResponse_APIGatewayProxyResponse_SetsMultiValueHeaders()
    {
        var apiResponse = new APIGatewayProxyResponse
        {
            MultiValueHeaders = new Dictionary<string, IList<string>>
            {
                { "X-Multi-Header", new List<string> { "Value1", "Value2" } }
            }
        };

        var httpResponse = apiResponse.ToHttpResponse();

        Assert.Equal(new[] { "Value1", "Value2" }, httpResponse.Headers["X-Multi-Header"]);
    }

    [Fact]
    public void ToHttpResponse_APIGatewayProxyResponse_SetsBodyNonBase64()
    {
        var apiResponse = new APIGatewayProxyResponse
        {
            Body = "Hello, World!",
            IsBase64Encoded = false
        };

        var httpResponse = apiResponse.ToHttpResponse();

        httpResponse.Body.Seek(0, SeekOrigin.Begin);
        var bodyContent = new StreamReader(httpResponse.Body).ReadToEnd();
        Assert.Equal("Hello, World!", bodyContent);
    }

    [Fact]
    public void ToHttpResponse_APIGatewayProxyResponse_SetsBodyBase64()
    {
        var originalBody = "Hello, World!";
        var base64Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalBody));
        var apiResponse = new APIGatewayProxyResponse
        {
            Body = base64Body,
            IsBase64Encoded = true
        };

        var httpResponse = apiResponse.ToHttpResponse();

        httpResponse.Body.Seek(0, SeekOrigin.Begin);
        var bodyContent = new StreamReader(httpResponse.Body).ReadToEnd();
        Assert.Equal(originalBody, bodyContent);
    }

    [Fact]
    public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_SetsCorrectStatusCode()
    {
        var apiResponse = new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 201
        };

        var httpResponse = apiResponse.ToHttpResponse();

        Assert.Equal(201, httpResponse.StatusCode);
    }

    [Fact]
    public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_SetsHeaders()
    {
        var apiResponse = new APIGatewayHttpApiV2ProxyResponse
        {
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/xml" },
                { "X-Custom-Header", "CustomValue" }
            }
        };

        var httpResponse = apiResponse.ToHttpResponse();

        Assert.Equal("application/xml", httpResponse.Headers["Content-Type"]);
        Assert.Equal("CustomValue", httpResponse.Headers["X-Custom-Header"]);
    }

    [Fact]
    public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_SetsCookies()
    {
        var apiResponse = new APIGatewayHttpApiV2ProxyResponse
        {
            Cookies = new[] { "session=abc123; Path=/", "theme=dark; Max-Age=3600" }
        };

        var httpResponse = apiResponse.ToHttpResponse();

        Assert.Contains(httpResponse.Headers["Set-Cookie"], v => v == "session=abc123; Path=/");
        Assert.Contains(httpResponse.Headers["Set-Cookie"], v => v == "theme=dark; Max-Age=3600");
    }

    [Fact]
    public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_SetsBodyNonBase64()
    {
        var apiResponse = new APIGatewayHttpApiV2ProxyResponse
        {
            Body = "Hello, API Gateway v2!",
            IsBase64Encoded = false
        };

        var httpResponse = apiResponse.ToHttpResponse();

        httpResponse.Body.Seek(0, SeekOrigin.Begin);
        var bodyContent = new StreamReader(httpResponse.Body).ReadToEnd();
        Assert.Equal("Hello, API Gateway v2!", bodyContent);
    }

    [Fact]
    public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_SetsBodyBase64()
    {
        var originalBody = "Hello, API Gateway v2!";
        var base64Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalBody));
        var apiResponse = new APIGatewayHttpApiV2ProxyResponse
        {
            Body = base64Body,
            IsBase64Encoded = true
        };

        var httpResponse = apiResponse.ToHttpResponse();

        httpResponse.Body.Seek(0, SeekOrigin.Begin);
        var bodyContent = new StreamReader(httpResponse.Body).ReadToEnd();
        Assert.Equal(originalBody, bodyContent);
    }
}
