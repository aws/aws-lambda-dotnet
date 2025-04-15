// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using Xunit;
using static Amazon.Lambda.TestTool.UnitTests.Extensions.ApiGatewayResponseTestCases;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions
{
    public class ApiGatewayResponseExtensionsTests
    {
        private ApiGatewayTestHelper _helper = new();

        [Theory]
        [MemberData(nameof(V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public async Task IntegrationTest_APIGatewayV1_REST(string testName, ApiGatewayResponseTestCase testCase)
        {
            await RunV1Test(testCase, ApiGatewayEmulatorMode.Rest, testName);
        }

        [Theory]
        [MemberData(nameof(V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public async Task IntegrationTest_APIGatewayV1_HTTP(string testName, ApiGatewayResponseTestCase testCase)
        {
            await RunV1Test(testCase, ApiGatewayEmulatorMode.HttpV1, testName);
        }

        [Theory]
        [MemberData(nameof(V2TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public async Task IntegrationTest_APIGatewayV2(string testName, ApiGatewayResponseTestCase testCase)
        {
            var testResponse = testCase.Response as APIGatewayHttpApiV2ProxyResponse;
            Assert.NotNull(testResponse);
            await _helper.VerifyHttpApiV2ResponseAsync(testResponse, testName);
        }

        [Fact]
        public async Task ToHttpResponse_RestAPIGatewayV1DecodesBase64()
        {
            var testResponse = new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = Convert.ToBase64String("test"u8.ToArray()),
                IsBase64Encoded = true
            };

            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            await testResponse.ToHttpResponseAsync(httpContext, ApiGatewayEmulatorMode.Rest);

            httpContext.Response.Body.Position = 0;

            Assert.Equal(200, (int)httpContext.Response.StatusCode);
            var content = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
            Assert.Equal("test", content);
        }

        [Fact]
        public async Task ToHttpResponse_HttpV1APIGatewayV1DecodesBase64()
        {
            var testResponse = new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = Convert.ToBase64String("test"u8.ToArray()),
                IsBase64Encoded = true
            };

            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            await testResponse.ToHttpResponseAsync(httpContext, ApiGatewayEmulatorMode.HttpV1);

            httpContext.Response.Body.Position = 0;

            Assert.Equal(200, (int)httpContext.Response.StatusCode);
            var content = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
            Assert.Equal("test", content);
        }

        private async Task RunV1Test(ApiGatewayResponseTestCase testCase, ApiGatewayEmulatorMode emulatorMode, string testName)
        {
            var testResponse = testCase.Response as APIGatewayProxyResponse;
            Assert.NotNull(testResponse);
            var testCaseName = testName + emulatorMode;
            await _helper.VerifyApiGatewayResponseAsync(testResponse, emulatorMode, testCaseName);

        }
    }
}
