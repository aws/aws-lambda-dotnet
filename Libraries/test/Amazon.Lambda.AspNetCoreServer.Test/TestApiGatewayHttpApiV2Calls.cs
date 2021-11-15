using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using TestWebApp;

using Xunit;



namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class TestApiGatewayHttpApiV2Calls
    {
        [Fact]
        public async Task TestValuesGetAllFromBetaStage()
        {
            var context = new TestLambdaContext();

            var response = await this.InvokeAPIGatewayRequest(context, "values-get-all-httpapi-v2-with-stage.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);

            Assert.Contains("OnStarting Called", ((TestLambdaLogger)context.Logger).Buffer.ToString());
        }

        [Fact]
        public async Task TestGetBinaryContent()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-binary-httpapi-v2-request.json");

            Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);

            string contentType;
            Assert.True(response.Headers.TryGetValue("Content-Type", out contentType),
                    "Content-Type response header exists");
            Assert.Equal("application/octet-stream", contentType);
            Assert.NotNull(response.Body);
            Assert.True(response.Body.Length > 0,
                "Body content is not empty");

            Assert.True(response.IsBase64Encoded, "Response IsBase64Encoded");

            // Compute a 256-byte array, with values 0-255
            var binExpected = new byte[byte.MaxValue].Select((val, index) => (byte)index).ToArray();
            var binActual = Convert.FromBase64String(response.Body);
            Assert.Equal(binExpected, binActual);
        }

        [Fact]
        public async Task TestEncodePlusInResourcePath()
        {
            var response = await this.InvokeAPIGatewayRequest("encode-plus-in-resource-path-httpapi-v2.json");

            Assert.Equal(200, response.StatusCode);

            var root = JObject.Parse(response.Body);
            Assert.Equal("/foo+bar", root["Path"]?.ToString());
        }

        [Fact]
        public async Task TestGetQueryStringValueMV()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-querystring-httpapi-v2-mv-request.json");

            Assert.Equal("value1,value2", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetEncodingQueryStringGateway()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-querystring-httpapi-v2-encoding-request.json");
            var results = JsonConvert.DeserializeObject<TestWebApp.Controllers.RawQueryStringController.Results>(response.Body);
            Assert.Equal("http://www.google.com", results.Url);
            Assert.Equal(DateTimeOffset.Parse("2019-03-12T16:06:06.549817+00:00"), results.TestDateTimeOffset);

            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestPutWithBody()
        {
            var response = await this.InvokeAPIGatewayRequest("values-put-withbody-httpapi-v2-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Agent, Smith", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestDefaultResponseErrorCode()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-error-httpapi-v2-request.json");

            Assert.Equal(500, response.StatusCode);
            Assert.Equal(string.Empty, response.Body);
        }

        [Theory]
        [InlineData("values-get-aggregateerror-httpapi-v2-request.json", "AggregateException")]
        [InlineData("values-get-typeloaderror-httpapi-v2-request.json", "ReflectionTypeLoadException")]
        public async Task TestEnhancedExceptions(string requestFileName, string expectedExceptionType)
        {
            var response = await this.InvokeAPIGatewayRequest(requestFileName);

            Assert.Equal(500, response.StatusCode);
            Assert.Equal(string.Empty, response.Body);
            Assert.True(response.Headers.ContainsKey("ErrorType"));
            Assert.Equal(expectedExceptionType, response.Headers["ErrorType"]);
        }

        [Fact]
        public async Task TestGettingSwaggerDefinition()
        {
            var response = await this.InvokeAPIGatewayRequest("swagger-get-httpapi-v2-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.True(response.Body.Length > 0);
            Assert.Equal("application/json", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestEncodeSpaceInResourcePath()
        {
            var response = await this.InvokeAPIGatewayRequest("encode-space-in-resource-path-httpapi-v2.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("value=tmh/file name.xml", response.Body);

        }

        [Fact]
        public async Task TestEncodeSlashInResourcePath()
        {
            var requestStr = GetRequestContent("encode-slash-in-resource-path-httpapi-v2.json");
            var response = await this.InvokeAPIGatewayRequestWithContent(new TestLambdaContext(), requestStr);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("{\"only\":\"a%2Fb\"}", response.Body);

            response = await this.InvokeAPIGatewayRequestWithContent(new TestLambdaContext(), requestStr.Replace("a%2Fb", "a/b"));

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("{\"first\":\"a\",\"second\":\"b\"}", response.Body);
        }

        [Fact]
        public async Task TestTrailingSlashInPath()
        {
            var response = await this.InvokeAPIGatewayRequest("trailing-slash-in-path-httpapi-v2.json");

            Assert.Equal(200, response.StatusCode);

            var root = JObject.Parse(response.Body);
            Assert.Equal("/beta", root["PathBase"]?.ToString());
            Assert.Equal("/foo/", root["Path"]?.ToString());
        }

        [Fact]
        public async Task TestAuthTestAccess()
        {
            var response = await this.InvokeAPIGatewayRequest("authtest-access-request-httpapi-v2.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("You Have Access", response.Body);
        }

        [Fact]
        public async Task TestAuthTestNoAccess()
        {
            var response = await this.InvokeAPIGatewayRequest("authtest-noaccess-request-httpapi-v2.json");

            Assert.NotEqual(200, response.StatusCode);
        }

        [Fact]
        public async Task TestAuthMTls()
        {
            var response = await this.InvokeAPIGatewayRequest("mtls-request-httpapi-v2.json");
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("O=Internet Widgits Pty Ltd, S=Some-State, C=AU", response.Body);
        }

        [Fact]
        public async Task TestReturningCookie()
        {
            var response = await this.InvokeAPIGatewayRequest("cookies-get-returned-httpapi-v2-request.json");

            Assert.Collection(response.Cookies,
                actual => Assert.StartsWith("TestCookie=TestValue", actual));
        }

        [Fact]
        public async Task TestReturningMultipleCookies()
        {
            var response = await this.InvokeAPIGatewayRequest("cookies-get-multiple-returned-httpapi-v2-request.json");

            Assert.Collection(response.Cookies.OrderBy(s => s),
                actual => Assert.StartsWith("TestCookie1=TestValue1", actual),
                actual => Assert.StartsWith("TestCookie2=TestValue2", actual));
        }

        [Fact]
        public async Task TestSingleCookie()
        {
            var response = await this.InvokeAPIGatewayRequest("cookies-get-single-httpapi-v2-request.json");

            Assert.Equal("TestValue", response.Body);
        }

        [Fact]
        public async Task TestMultipleCookie()
        {
            var response = await this.InvokeAPIGatewayRequest("cookies-get-multiple-httpapi-v2-request.json");

            Assert.Equal("TestValue3", response.Body);
        }

        private async Task<APIGatewayHttpApiV2ProxyResponse> InvokeAPIGatewayRequest(string fileName)
        {
            return await InvokeAPIGatewayRequestWithContent(new TestLambdaContext(), GetRequestContent(fileName));
        }

        private async Task<APIGatewayHttpApiV2ProxyResponse> InvokeAPIGatewayRequest(TestLambdaContext context, string fileName)
        {
            return await InvokeAPIGatewayRequestWithContent(context, GetRequestContent(fileName));
        }

        private async Task<APIGatewayHttpApiV2ProxyResponse> InvokeAPIGatewayRequestWithContent(TestLambdaContext context, string requestContent)
        {
            var lambdaFunction = new TestWebApp.HttpV2LambdaFunction();
            var requestStream = new MemoryStream(System.Text.UTF8Encoding.UTF8.GetBytes(requestContent));
            var request = new Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer().Deserialize<APIGatewayHttpApiV2ProxyRequest>(requestStream);

            return await lambdaFunction.FunctionHandlerAsync(request, context);
        }

        private string GetRequestContent(string fileName)
        {
            var filePath = Path.Combine(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location), fileName);
            var requestStr = File.ReadAllText(filePath);
            return requestStr;
        }
    }
}
