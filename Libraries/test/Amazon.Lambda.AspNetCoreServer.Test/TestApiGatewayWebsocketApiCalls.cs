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
    public class TestApiGatewayWebsocketApiCalls
    {
        [Fact]
        public async Task TestPostWithBody()
        {
            var response = await this.InvokeAPIGatewayRequest("values-post-withbody-websocketapi-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Agent, Smith", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);
        }

        private async Task<APIGatewayProxyResponse> InvokeAPIGatewayRequest(string fileName, bool configureApiToReturnExceptionDetail = false)
        {
            return await InvokeAPIGatewayRequestWithContent(new TestLambdaContext(), GetRequestContent(fileName), configureApiToReturnExceptionDetail);
        }

        private async Task<APIGatewayProxyResponse> InvokeAPIGatewayRequest(TestLambdaContext context, string fileName, bool configureApiToReturnExceptionDetail = false)
        {
            return await InvokeAPIGatewayRequestWithContent(context, GetRequestContent(fileName), configureApiToReturnExceptionDetail);
        }

        private async Task<APIGatewayProxyResponse> InvokeAPIGatewayRequestWithContent(TestLambdaContext context, string requestContent, bool configureApiToReturnExceptionDetail = false)
        {
            var lambdaFunction = new TestWebApp.WebsocketLambdaFunction();
            if (configureApiToReturnExceptionDetail)
                lambdaFunction.IncludeUnhandledExceptionDetailInResponse = true;
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
