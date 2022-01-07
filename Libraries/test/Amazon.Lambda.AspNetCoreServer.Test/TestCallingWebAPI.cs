using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;

using TestWebApp;

using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class TestCallingWebAPI
    {
        public TestCallingWebAPI()
        {
        }


        [Fact]
        public async Task TestHttpApiGetAllValues()
        {
            var context = new TestLambdaContext();

            var response = await this.InvokeAPIGatewayRequest(context, "values-get-all-httpapi-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);

            Assert.Contains("OnStarting Called", ((TestLambdaLogger)context.Logger).Buffer.ToString());
        }


        [Fact]
        public async Task TestGetAllValues()
        {
            var context = new TestLambdaContext();

            var response = await this.InvokeAPIGatewayRequest(context, "values-get-all-apigateway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);

            Assert.Contains("OnStarting Called", ((TestLambdaLogger) context.Logger).Buffer.ToString());
        }

        [Fact]
        public async Task TestGetAllValuesWithCustomPath()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-different-proxypath-apigateway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestGetSingleValue()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-single-apigateway-request.json");

            Assert.Equal("value=5", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestGetQueryStringValue()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-querystring-apigateway-request.json");

            Assert.Equal("Lewis, Meriwether", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestGetNoQueryStringApiGateway()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-no-querystring-apigateway-request.json");

            Assert.Equal(string.Empty, response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestGetEncodingQueryStringGateway()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-querystring-apigateway-encoding-request.json");
            var results = JsonSerializer.Deserialize<TestWebApp.Controllers.RawQueryStringController.Results>(response.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            Assert.Equal("http://www.gooogle.com", results.Url);
            Assert.Equal(DateTimeOffset.Parse("2019-03-12T16:06:06.549817+00:00"), results.TestDateTimeOffset);

            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestPutWithBody()
        {
            var response = await this.InvokeAPIGatewayRequest("values-put-withbody-apigateway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Agent, Smith", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestDefaultResponseErrorCode()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-error-apigateway-request.json");

            Assert.Equal(500, response.StatusCode);
            Assert.Equal(string.Empty, response.Body);
        }

        [Theory]
        [InlineData("values-get-aggregateerror-apigateway-request.json", "AggregateException")]
        [InlineData("values-get-typeloaderror-apigateway-request.json", "ReflectionTypeLoadException")]
        public async Task TestEnhancedExceptions(string requestFileName, string expectedExceptionType)
        {
            var response = await this.InvokeAPIGatewayRequest(requestFileName);

            Assert.Equal(500, response.StatusCode);
            Assert.Equal(string.Empty, response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("ErrorType"));
            Assert.Equal(expectedExceptionType, response.MultiValueHeaders["ErrorType"][0]);
        }

        [Fact]
        public async Task TestGettingSwaggerDefinition()
        {
            var response = await this.InvokeAPIGatewayRequest("swagger-get-apigateway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.True(response.Body.Length > 0);
            Assert.Equal("application/json", response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public void TestGetCustomAuthorizerValue()
        {
            var requestStr = File.ReadAllText("values-get-customauthorizer-apigateway-request.json");
            var request = JsonSerializer.Deserialize<APIGatewayProxyRequest>(requestStr, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            Assert.NotNull(request?.RequestContext.Authorizer);
            Assert.NotNull(request.RequestContext.Authorizer.StringKey);
            Assert.Equal(9, request.RequestContext.Authorizer.NumKey);
            Assert.True(request.RequestContext.Authorizer.BoolKey);
            Assert.NotEmpty(request.RequestContext.Authorizer.Claims);
            Assert.Equal("test-id", request.RequestContext.Authorizer.Claims["sub"]);
        }

        [Fact]
        public void TestCustomAuthorizerSerialization()
        {
            var response = new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = "com.amazon.someuser",
                Context = new APIGatewayCustomAuthorizerContextOutput
                {
                    StringKey = "Hey I'm a string",
                    BoolKey = true,
                    NumKey = 9
                },
                PolicyDocument = new APIGatewayCustomAuthorizerPolicy
                {
                    Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                    {
                        new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                        {
                            Effect = "Allow",
                            Action = new HashSet<string> {"execute-api:Invoke"},
                            Resource = new HashSet<string>
                                {"arn:aws:execute-api:us-west-2:1234567890:apit123d45/Prod/GET/*"}
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            Assert.NotNull(json);
            var expected = "{\"principalId\":\"com.amazon.someuser\",\"policyDocument\":{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Action\":[\"execute-api:Invoke\"],\"Resource\":[\"arn:aws:execute-api:us-west-2:1234567890:apit123d45/Prod/GET/*\"]}]},\"context\":{\"stringKey\":\"Hey I'm a string\",\"boolKey\":true,\"numKey\":9},\"usageIdentifierKey\":null}";
            Assert.Equal(expected, json);
        }

        [Fact]
        public async Task TestGetBinaryContent()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-binary-apigateway-request.json");

            Assert.Equal((int) HttpStatusCode.OK, response.StatusCode);

            IList<string> contentType;
            Assert.True(response.MultiValueHeaders.TryGetValue("Content-Type", out contentType),
                    "Content-Type response header exists");
            Assert.Equal("application/octet-stream", contentType[0]);
            Assert.NotNull(response.Body);
            Assert.True(response.Body.Length > 0,
                "Body content is not empty");

            Assert.True(response.IsBase64Encoded, "Response IsBase64Encoded");

            // Compute a 256-byte array, with values 0-255
            var binExpected = new byte[byte.MaxValue].Select((val, index) => (byte) index).ToArray();
            var binActual = Convert.FromBase64String(response.Body);
            Assert.Equal(binExpected, binActual);
        }

        [Fact]
        public async Task TestEncodePlusInResourcePath()
        {
            var response = await this.InvokeAPIGatewayRequest("encode-plus-in-resource-path.json");

            Assert.Equal(200, response.StatusCode);

            var root = JsonSerializer.Deserialize<IDictionary<string, object>>(response.Body);
            Assert.Equal("/foo+bar", root?["Path"]?.ToString());
        }

        [Fact]
        public async Task TestEncodeSpaceInResourcePath()
        {
            var requestStr = GetRequestContent("encode-space-in-resource-path.json");
            var response = await this.InvokeAPIGatewayRequest("encode-space-in-resource-path.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("value=tmh/file name.xml", response.Body);

        }

        [Fact]
        public async Task TestEncodeSlashInResourcePath()
        {
            var requestStr = GetRequestContent("encode-slash-in-resource-path.json");
            var response = await this.InvokeAPIGatewayRequestWithContent(new TestLambdaContext(), requestStr);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("{\"only\":\"a%2Fb\"}", response.Body);

            response = await this.InvokeAPIGatewayRequestWithContent(new TestLambdaContext(), requestStr.Replace("a%2Fb", "a/b"));

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("{\"first\":\"a\",\"second\":\"b\"}", response.Body);
        }

        [Fact]
        public async Task TestSpaceInResourcePathAndQueryString()
        {
            var response = await this.InvokeAPIGatewayRequest("encode-space-in-resource-path-and-query.json");

            Assert.Equal(200, response.StatusCode);

            var root = JsonSerializer.Deserialize<JsonObject>(response.Body);
            Assert.Equal("/foo%20bar", root?["Path"]?.ToString());

            var query = root["QueryVariables"]["greeting"] as JsonArray;
            Assert.Equal("hello world", query[0].ToString());
        }

        [Fact]
        public async Task TestTrailingSlashInPath()
        {
            var response = await this.InvokeAPIGatewayRequest("trailing-slash-in-path.json");

            Assert.Equal(200, response.StatusCode);

            var root = JsonSerializer.Deserialize<JsonObject>(response.Body);
            Assert.Equal("/Prod", root?["PathBase"]?.ToString());
            Assert.Equal("/foo/", root?["Path"]?.ToString());
        }

        [Fact]
        public async Task TestAuthTestAccess()
        {
            var response = await this.InvokeAPIGatewayRequest("authtest-access-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("You Have Access", response.Body);
        }

        [Fact]
        public async Task TestAuthMTls()
        {
            var response = await this.InvokeAPIGatewayRequest("mtls-request.json");
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("O=Internet Widgits Pty Ltd, S=Some-State, C=AU", response.Body);
        }

        [Fact]
        public async Task TestAuthMTlsWithTrailingNewLine()
        {
            var response = await this.InvokeAPIGatewayRequest("mtls-request-trailing-newline.json");
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("O=Internet Widgits Pty Ltd, S=Some-State, C=AU", response.Body);
        }

        [Fact]
        public async Task TestAuthTestAccess_CustomLambdaAuthorizerClaims()
        {
            var response =
                await this.InvokeAPIGatewayRequest("authtest-access-request-custom-lambda-authorizer-output.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("You Have Access", response.Body);
        }

        [Fact]
        public async Task TestAuthTestNoAccess()
        {
            var response = await this.InvokeAPIGatewayRequest("authtest-noaccess-request.json");

            Assert.NotEqual(200, response.StatusCode);
        }

        // Covers the test case when using a custom proxy request, probably for testing, and doesn't specify the resource
        [Fact]
        public async Task TestMissingResourceInRequest()
        {
            var response = await this.InvokeAPIGatewayRequest("missing-resource-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.True(response.Body.Length > 0);
            Assert.Contains("application/json", response.MultiValueHeaders["Content-Type"][0]);
        }

        // If there is no content-type we must make sure Content-Type is set to null in the headers collection so API Gateway doesn't return a default Content-Type.
        [Fact]
        public async Task TestDeleteNoContentContentType()
        {
            var response = await this.InvokeAPIGatewayRequest("values-delete-no-content-type-apigateway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.True(response.Body.Length == 0);
            Assert.Equal(1, response.MultiValueHeaders["Content-Type"].Count);
            Assert.Null(response.MultiValueHeaders["Content-Type"][0]);
        }

        [Fact]
        public async Task TestRedirectNoContentType()
        {
            var response = await this.InvokeAPIGatewayRequest("redirect-apigateway-request.json");

            Assert.Equal(302, response.StatusCode);
            Assert.True(response.Body.Length == 0);
            Assert.Equal(1, response.MultiValueHeaders["Content-Type"].Count);
            Assert.Null(response.MultiValueHeaders["Content-Type"][0]);

            Assert.Equal("redirecttarget", response.MultiValueHeaders["Location"][0]);
        }

        [Fact]
        public async Task TestContentLengthWithContent()
        {
            var response = await this.InvokeAPIGatewayRequest("check-content-length-withcontent-apigateway.json");
            Assert.Equal("Request content length: 17", response.Body.Trim());
        }

        [Fact]
        public async Task TestContentLengthNoContent()
        {
            var response = await this.InvokeAPIGatewayRequest("check-content-length-nocontent-apigateway.json");
            Assert.Equal("Request content length: 0", response.Body.Trim());
        }

        [Fact]
        public async Task TestGetCompressResponse()
        {
            var context = new TestLambdaContext();

            var response = await this.InvokeAPIGatewayRequest(context, "compressresponse-get-apigateway-request.json");

            Assert.Equal(200, response.StatusCode);

            var bytes = Convert.FromBase64String(response.Body);
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    gs.CopyTo(mso);

                }
                var body = UTF8Encoding.UTF8.GetString(mso.ToArray());
                Assert.Equal("[\"value1\",\"value2\"]", body);
            }

            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("application/json-compress", response.MultiValueHeaders["Content-Type"][0]);
            Assert.Equal("gzip", response.MultiValueHeaders["Content-Encoding"][0]);

            Assert.Contains("OnStarting Called", ((TestLambdaLogger)context.Logger).Buffer.ToString());
        }

        [Fact]
        public async Task TestRequestServicesAreAvailable()
        {
            var requestStr = GetRequestContent("requestservices-get-apigateway-request.json");
            var response = await this.InvokeAPIGatewayRequestWithContent(new TestLambdaContext(), requestStr);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Microsoft.Extensions.DependencyInjection.ServiceLookup.ServiceProviderEngineScope", response.Body);
        }

        private async Task<APIGatewayProxyResponse> InvokeAPIGatewayRequest(string fileName)
        {
            return await InvokeAPIGatewayRequest(new TestLambdaContext(), fileName);
        }

        private async Task<APIGatewayProxyResponse> InvokeAPIGatewayRequest(TestLambdaContext context, string fileName)
        {
            return await InvokeAPIGatewayRequestWithContent(context, GetRequestContent(fileName));
        }

        private async Task<APIGatewayProxyResponse> InvokeAPIGatewayRequestWithContent(TestLambdaContext context, string requestContent)
        {
            var lambdaFunction = new ApiGatewayLambdaFunction();
            var requestStream = new MemoryStream(System.Text.UTF8Encoding.UTF8.GetBytes(requestContent));
            var request = new Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer().Deserialize<APIGatewayProxyRequest>(requestStream);

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