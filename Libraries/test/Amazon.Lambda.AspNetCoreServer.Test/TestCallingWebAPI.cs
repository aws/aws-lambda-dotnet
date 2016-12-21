using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using TestWebApp;
using Xunit;

using Newtonsoft.Json;

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
            var lambdaFunction = new LambdaFunction();

            var requestStr = File.ReadAllText("values-get-all-apigatway-request.json");
            var request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            var response = await lambdaFunction.FunctionHandlerAsync(request, null);

            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetSingleValue()
        {
            var lambdaFunction = new LambdaFunction();

            var requestStr = File.ReadAllText("values-get-single-apigatway-request.json");
            var request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            var response = await lambdaFunction.FunctionHandlerAsync(request, null);

            Assert.Equal("value=5", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestGetQueryStringValue()
        {
            var lambdaFunction = new LambdaFunction();

            var requestStr = File.ReadAllText("values-get-querystring-apigatway-request.json");
            var request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            var response = await lambdaFunction.FunctionHandlerAsync(request, null);

            Assert.Equal("Lewis, Meriwether", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        [Fact]
        public async Task TestPutWithBody()
        {
            var lambdaFunction = new LambdaFunction();

            var requestStr = File.ReadAllText("values-put-withbody-apigatway-request.json");
            var request = JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
            var response = await lambdaFunction.FunctionHandlerAsync(request, null);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Agent, Smith", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }
    }
}
