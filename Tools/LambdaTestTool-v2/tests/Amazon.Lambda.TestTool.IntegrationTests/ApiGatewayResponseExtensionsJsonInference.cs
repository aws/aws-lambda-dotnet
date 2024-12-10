using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.ApiGatewayV2;
using Amazon.Lambda;
using Amazon.IdentityManagement;
using Xunit;
using Amazon.Lambda.APIGatewayEvents;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Amazon.Lambda.TestTool.Extensions;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    public class ApiGatewayResponseExtensionsJsonInference : IAsyncLifetime
    {
        private readonly ApiGatewayTestHelper _helper;
        private string _httpApiV2Id;
        private string _lambdaArn;
        private string _httpApiV2Url;
        private string _roleArn;
        private readonly HttpClient _httpClient;

        public ApiGatewayResponseExtensionsJsonInference()
        {
            var apiGatewayV2Client = new AmazonApiGatewayV2Client(RegionEndpoint.USWest2);
            var lambdaClient = new AmazonLambdaClient(RegionEndpoint.USWest2);
            var iamClient = new AmazonIdentityManagementServiceClient(RegionEndpoint.USWest2);

            _helper = new ApiGatewayTestHelper(null, apiGatewayV2Client, lambdaClient, iamClient);
            _httpClient = new HttpClient();
        }

        public async Task InitializeAsync()
        {
            // Create IAM Role for Lambda
            _roleArn = await _helper.CreateIamRoleAsync();

            // Create Lambda function
            var lambdaCode = @"
            exports.handler = async (event, context, callback) => {
                console.log(event);
                callback(null, event.body);
            };";
            _lambdaArn = await _helper.CreateLambdaFunctionAsync(_roleArn, lambdaCode);

            // Create HTTP API (v2)
            (_httpApiV2Id, _httpApiV2Url) = await _helper.CreateHttpApi(_lambdaArn, "2.0");

            // Wait for the API Gateway to propagate
            await Task.Delay(10000); // Wait for 10 seconds

            // Grant API Gateway permission to invoke Lambda
            await _helper.GrantApiGatewayPermissionToLambda(_lambdaArn);
        }

        [Fact]
        public async Task V2_TestCanInferJsonType()
        {

            var testResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = "Hello from lambda" // a regular string is considered json in api gateway
            };

            var httpTestResponse = testResponse.ToHttpResponse();
            var actualResponse = await _httpClient.PostAsync(_httpApiV2Url, new StringContent("Hello from lambda"));

            await _helper.AssertResponsesEqual(actualResponse, httpTestResponse);
            Assert.Equal(200, (int)actualResponse.StatusCode);
            Assert.Equal("application/json", actualResponse.Content.Headers.ContentType.ToString());
            var content = await actualResponse.Content.ReadAsStringAsync();
            Assert.Equal("Hello from lambda", content);
        }

        [Fact]
        public async Task V2_TestCanInferJsonType2()
        {

            var testResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = "{\"key\" : \"value\"}"
            };

            var httpTestResponse = testResponse.ToHttpResponse();
            var actualResponse = await _httpClient.PostAsync(_httpApiV2Url, new StringContent("{\"key\" : \"value\"}"));

            await _helper.AssertResponsesEqual(actualResponse, httpTestResponse);
            Assert.Equal(200, (int)actualResponse.StatusCode);
            Assert.Equal("application/json", actualResponse.Content.Headers.ContentType.ToString());
            var content = await actualResponse.Content.ReadAsStringAsync();
            Assert.Equal("{\"key\" : \"value\"}", content);
        }

        [Fact]
        public async Task V2_HandlesNonJsonResponse()
        {
            var testResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = "{\"key\"}"
            };

            var httpTestResponse = testResponse.ToHttpResponse();
            var actualResponse = await _httpClient.PostAsync(_httpApiV2Url, new StringContent("{\"key\"}"));

            await _helper.AssertResponsesEqual(actualResponse, httpTestResponse);
            Assert.Equal(500, (int)actualResponse.StatusCode);
            Assert.Equal("application/json", actualResponse.Content.Headers.ContentType.ToString());
        }

        public async Task DisposeAsync()
        {
            await _helper.CleanupResources(null, null, _httpApiV2Id, _lambdaArn, _roleArn);
        }
    }
}
