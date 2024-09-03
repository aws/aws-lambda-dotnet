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
    public class TestApiGatewayWebsocketApiV2Calls
    {
        [Fact]
        public async Task TestPutWithBody()
        {
            var response = await this.InvokeAPIGatewayRequest("values-post-withbody-websocketapi-v2-request.json");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("Agent, Smith", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);
        }

        private async Task<APIGatewayHttpApiV2ProxyResponse> InvokeAPIGatewayRequest(string fileName, bool configureApiToReturnExceptionDetail = false)
        {
            return await InvokeAPIGatewayRequestWithContent(new TestLambdaContext(), GetRequestContent(fileName), configureApiToReturnExceptionDetail);
        }

        private async Task<APIGatewayHttpApiV2ProxyResponse> InvokeAPIGatewayRequest(TestLambdaContext context, string fileName, bool configureApiToReturnExceptionDetail = false)
        {
            return await InvokeAPIGatewayRequestWithContent(context, GetRequestContent(fileName), configureApiToReturnExceptionDetail);
        }

        private async Task<APIGatewayHttpApiV2ProxyResponse> InvokeAPIGatewayRequestWithContent(TestLambdaContext context, string requestContent, bool configureApiToReturnExceptionDetail = false)
        {
            var lambdaFunction = new TestWebApp.WebsocketV2LambdaFunction();
            if (configureApiToReturnExceptionDetail)
                lambdaFunction.IncludeUnhandledExceptionDetailInResponse = true;
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
