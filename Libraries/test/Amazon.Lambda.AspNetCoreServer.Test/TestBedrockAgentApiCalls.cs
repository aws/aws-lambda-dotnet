using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.BedrockAgentEvents;
using Amazon.Lambda.TestUtilities;
using TestWebApp;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class TestBedrockAgentApiCalls
    {
        [Fact]
        public async Task TestGetAllValues()
        {
            var context = new TestLambdaContext();

            var response = await InvokeBedrockAgentApiRequest(context, "values-get-all-bedrock-agent-request.json");

            Assert.Equal(200, response.Response.HttpStatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Response.ResponseBody["application/json"].Body);
            Assert.Equal("ValuesApi", response.Response.ActionGroup);
            Assert.Equal("/api/values", response.Response.ApiPath);
            Assert.Equal("GET", response.Response.HttpMethod);
            Assert.Equal("1.0", response.MessageVersion);
            Assert.Contains("key1", response.SessionAttributes.Keys);
            Assert.Equal("value1", response.SessionAttributes["key1"]);
            Assert.Contains("key2", response.PromptSessionAttributes.Keys);
            Assert.Equal("value2", response.PromptSessionAttributes["key2"]);

            Assert.Contains("OnStarting Called", ((TestLambdaLogger)context.Logger).Buffer.ToString());
        }

        [Fact]
        public async Task TestGetSingleValue()
        {
            var response = await InvokeBedrockAgentApiRequest("values-get-single-bedrock-agent-request.json");

            Assert.Equal(200, response.Response.HttpStatusCode);
            Assert.Equal("value", response.Response.ResponseBody["text/plain; charset=utf-8"].Body);
            Assert.Equal("ValuesApi", response.Response.ActionGroup);
            Assert.Equal("/api/values/5", response.Response.ApiPath);
            Assert.Equal("GET", response.Response.HttpMethod);
        }

        [Fact]
        public async Task TextGetPathParamValue()
        {
            var response = await InvokeBedrockAgentApiRequest("values-get-pathparam-bedrock-agent-request.json");

            Assert.Equal(200, response.Response.HttpStatusCode);
            Assert.Equal("value=testid", response.Response.ResponseBody["text/plain; charset=utf-8"].Body);
            Assert.Equal("PathParamApi", response.Response.ActionGroup);
            Assert.Equal("/api/resourcepath/{id}", response.Response.ApiPath);
            Assert.Equal("GET", response.Response.HttpMethod);
        }

        [Fact]
        public async Task TestGetQueryStringValue()
        {
            var response = await InvokeBedrockAgentApiRequest("values-get-querystring-bedrock-agent-request.json");

            Assert.Equal(200, response.Response.HttpStatusCode);
            Assert.Equal("Lewis, Meriwether", response.Response.ResponseBody["text/plain; charset=utf-8"].Body);
            Assert.Equal("ValuesApi", response.Response.ActionGroup);
            Assert.Equal("/api/querystring", response.Response.ApiPath);
            Assert.Equal("GET", response.Response.HttpMethod);
        }

        [Fact]
        public async Task TestPutWithBody()
        {
            var response = await InvokeBedrockAgentApiRequest("values-put-withbody-bedrock-agent-request.json");

            Assert.Equal(200, response.Response.HttpStatusCode);
            Assert.Equal("Agent, Smith", response.Response.ResponseBody["text/plain; charset=utf-8"].Body);
            Assert.Equal("ValuesApi", response.Response.ActionGroup);
            Assert.Equal("/api/bodytests", response.Response.ApiPath);
            Assert.Equal("PUT", response.Response.HttpMethod);
        }

        private async Task<BedrockAgentApiResponse> InvokeBedrockAgentApiRequest(string fileName)
        {
            return await InvokeBedrockAgentApiRequest(new TestLambdaContext(), fileName);
        }

        private async Task<BedrockAgentApiResponse> InvokeBedrockAgentApiRequest(TestLambdaContext context, string fileName)
        {
            var requestContent = GetRequestContent(fileName);
            return await InvokeBedrockAgentApiRequestWithContent(context, requestContent);
        }

        private async Task<BedrockAgentApiResponse> InvokeBedrockAgentApiRequestWithContent(TestLambdaContext context, string requestContent)
        {
            var function = new TestBedrockAgentApiFunction();
            if (function.IncludeUnhandledExceptionDetailInResponse == false)
                function.IncludeUnhandledExceptionDetailInResponse = true;
            var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(requestContent));
            var request = new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer().Deserialize<BedrockAgentApiRequest>(requestStream);

            return await function.FunctionHandlerAsync(request, context);
        }

        private string GetRequestContent(string fileName)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (!File.Exists(filePath))
            {
                filePath = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), fileName);
            }
            return File.ReadAllText(filePath);
        }
    }

    /// <summary>
    /// Test implementation of the BedrockAgentApiFunction
    /// </summary>
    public class TestBedrockAgentApiFunction : BedrockAgentApiFunction<Startup>
    {
        public TestBedrockAgentApiFunction()
            : base(StartupMode.Constructor)
        {
            IncludeUnhandledExceptionDetailInResponse = true;
        }
    }
}
