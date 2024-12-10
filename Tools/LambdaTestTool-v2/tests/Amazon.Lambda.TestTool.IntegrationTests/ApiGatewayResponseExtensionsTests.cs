using System;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Net.Http;
using Amazon.APIGateway;
using Amazon.APIGateway.Model;
using Amazon.ApiGatewayV2;
using Amazon.ApiGatewayV2.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Xunit;
using Microsoft.AspNetCore.Http;
using static ApiGatewayResponseTestCases;
using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    public class ApiGatewayResponseExtensionsTests : IAsyncLifetime
    {
        private readonly ApiGatewayTestHelper _helper;
        private string _restApiId;
        private string _httpApiV1Id;
        private string _httpApiV2Id;
        private string _lambdaArn;
        private string _restApiUrl;
        private string _httpApiV1Url;
        private string _httpApiV2Url;
        private string _roleArn;


        public ApiGatewayResponseExtensionsTests()
        {
            var apiGatewayV1Client = new AmazonAPIGatewayClient(RegionEndpoint.USWest2);
            var apiGatewayV2Client = new AmazonApiGatewayV2Client(RegionEndpoint.USWest2);
            var lambdaClient = new AmazonLambdaClient(RegionEndpoint.USWest2);
            var iamClient = new AmazonIdentityManagementServiceClient(RegionEndpoint.USWest2);

            _helper = new ApiGatewayTestHelper(apiGatewayV1Client, apiGatewayV2Client, lambdaClient, iamClient);
        }

        public async Task InitializeAsync()
        {

            // Create IAM Role for Lambda
            _roleArn = await _helper.CreateIamRoleAsync();

            // Create Lambda function
            var lambdaCode = @"
                exports.handler = async (event) => {
                    console.log(event);
                    console.log(event.body);
                    const j = JSON.parse(event.body);
                    console.log(j);
                    return j;
            };";
            _lambdaArn = await _helper.CreateLambdaFunctionAsync(_roleArn, lambdaCode);

            // Create REST API (v1)
            (_restApiId, _restApiUrl) = await _helper.CreateRestApiV1(_lambdaArn);

            // Create HTTP API (v1)
            (_httpApiV1Id, _httpApiV1Url) = await _helper.CreateHttpApi(_lambdaArn, "1.0");

            // Create HTTP API (v2)
            (_httpApiV2Id, _httpApiV2Url) = await _helper.CreateHttpApi(_lambdaArn, "2.0");

            // Wait for the API Gateway to propagate
            await Task.Delay(10000); // Wait for 10 seconds

            // Grant API Gateway permission to invoke Lambda
            await _helper.GrantApiGatewayPermissionToLambda(_lambdaArn);
        }


        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public async Task IntegrationTest_APIGatewayV1_REST(string testName, ApiGatewayResponseTestCase testCase)
        {
            await RunV1Test(testName, testCase, _restApiUrl, ApiGatewayEmulatorMode.Rest);
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public async Task IntegrationTest_APIGatewayV1_HTTP(string testName, ApiGatewayResponseTestCase testCase)
        {
            await RunV1Test(testName, testCase, _httpApiV1Url, ApiGatewayEmulatorMode.HttpV1);
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V2TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public async Task IntegrationTest_APIGatewayV2(string testName, ApiGatewayResponseTestCase testCase)
        {
            var testResponse = testCase.Response as APIGatewayHttpApiV2ProxyResponse;

            var (actualResponse, httpTestResponse) = await _helper.ExecuteTestRequest(testResponse, _httpApiV2Url);

            await _helper.AssertResponsesEqual(actualResponse, httpTestResponse);
            await testCase.IntegrationAssertions(actualResponse, Models.ApiGatewayEmulatorMode.HttpV2);
            await Task.Delay(10000); // Wait for 10 seconds
        }

        private async Task RunV1Test(string testName, ApiGatewayResponseTestCase testCase, string apiUrl, ApiGatewayEmulatorMode emulatorMode)
        {
            var testResponse = testCase.Response as APIGatewayProxyResponse;
            var (actualResponse, httpTestResponse) = await _helper.ExecuteTestRequest(testResponse, apiUrl, emulatorMode);

            await _helper.AssertResponsesEqual(actualResponse, httpTestResponse);
            await testCase.IntegrationAssertions(actualResponse, emulatorMode);
            await Task.Delay(10000); // Wait for 10 seconds
        }

        public async Task DisposeAsync()
        {
            // Clean up resources
            await _helper.CleanupResources(_restApiId, _httpApiV1Id, _httpApiV2Id, _lambdaArn, _roleArn);
        }
    }
}
