using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestWebApp;
using Xunit;
using System;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class TestCallingWebAPI
    {
        public TestCallingWebAPI()
        {
        }

        [Fact]
        public async Task TestGetAllValues()
        {
            var context = new TestLambdaContext();

            var response = await this.InvokeAPIGatewayRequest(context, "values-get-all-apigatway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);

            Assert.Contains("OnStarting Called", ((TestLambdaLogger)context.Logger).Buffer.ToString());
        }

        [Fact]
        public async Task TestGetAllValuesWithCustomPath()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-different-proxypath-apigatway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetSingleValue()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-single-apigatway-request.json");

            Assert.Equal("value=5", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetQueryStringValue()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-querystring-apigatway-request.json");

            Assert.Equal("Lewis, Meriwether", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetQueryStringWithSpacesValue()
        {
            var response = await this.InvokeAPIGatewayRequest("values-escape-querystring-apigatway-request.json");

            Assert.Equal("Norman Ivar, Johanson", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestPutWithBody()
        {
            var response = await this.InvokeAPIGatewayRequest("values-put-withbody-apigatway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Agent, Smith", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestDefaultResponseErrorCode()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-error-apigatway-request.json");

            Assert.Equal(500, response.StatusCode);
            Assert.Equal(string.Empty, response.Body);
        }

        [Theory]
        [InlineData("values-get-aggregateerror-apigatway-request.json", "AggregateException")]
        [InlineData("values-get-typeloaderror-apigatway-request.json", "ReflectionTypeLoadException")]
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
            var response = await this.InvokeAPIGatewayRequest("swagger-get-apigatway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.True(response.Body.Length > 0);
            Assert.Equal("application/json", response.Headers["Content-Type"]);
        }

        [Fact]
        public void TestGetCustomAuthorizerValue()
        {
            var requestStr = File.ReadAllText("values-get-customauthorizer-apigatway-request.json");
            var request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            Assert.NotNull(request.RequestContext.Authorizer);
            Assert.NotNull(request.RequestContext.Authorizer.StringKey);
            Assert.Equal(9, request.RequestContext.Authorizer.NumKey);
            Assert.True(request.RequestContext.Authorizer.BoolKey);
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
                            Action = new HashSet<string> { "execute-api:Invoke" },
                            Resource = new HashSet<string> { "arn:aws:execute-api:us-west-2:1234567890:apit123d45/Prod/GET/*" }
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(response);
            Assert.NotNull(json);
            var expected = "{\"principalId\":\"com.amazon.someuser\",\"policyDocument\":{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Action\":[\"execute-api:Invoke\"],\"Resource\":[\"arn:aws:execute-api:us-west-2:1234567890:apit123d45/Prod/GET/*\"]}]},\"context\":{\"stringKey\":\"Hey I'm a string\",\"boolKey\":true,\"numKey\":9}}";
            Assert.Equal(expected, json);
        }

        [Fact]
        public async Task TestGetBinaryContent()
        {
            var response = await this.InvokeAPIGatewayRequest("values-get-binary-apigatway-request.json");
            
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
        public async Task TestEscapeCharacterInResourcePath()
        {
            var response = await this.InvokeAPIGatewayRequest("values-escape-path-apigatway-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("value=query string", response.Body);
        }

        [Fact]
        public async Task TestAuthTestAccess()
        {
            var response = await this.InvokeAPIGatewayRequest("authtest-access-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("You Have Access", response.Body);
        }


        [Fact]
        public async Task TestAuthTestNoAccess()
        {
            var response = await this.InvokeAPIGatewayRequest("authtest-noaccess-request.json");

            Assert.NotEqual(200, response.StatusCode);
        }

        private async Task<APIGatewayProxyResponse> InvokeAPIGatewayRequest(string fileName)
        {
            return await InvokeAPIGatewayRequest(new TestLambdaContext(), fileName);
        }

        private async Task<APIGatewayProxyResponse> InvokeAPIGatewayRequest(TestLambdaContext context, string fileName)
        {
            var lambdaFunction = new LambdaFunction();
            var filePath = Path.Combine(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location), fileName);
            var requestStr = File.ReadAllText(filePath);
            var request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            return await lambdaFunction.FunctionHandlerAsync(request, context);
        }
    }
}
