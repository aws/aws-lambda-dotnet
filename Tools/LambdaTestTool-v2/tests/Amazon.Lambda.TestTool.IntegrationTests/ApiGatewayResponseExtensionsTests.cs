// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.IntegrationTests.Helpers;
using Amazon.Lambda.TestTool.Models;
using static ApiGatewayResponseTestCases;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    [Collection("ApiGateway Integration Tests")]
    public class ApiGatewayResponseExtensionsTests
    {
        private readonly ApiGatewayIntegrationTestFixture _fixture;

        public ApiGatewayResponseExtensionsTests(ApiGatewayIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task IntegrationTest_APIGatewayV1_REST(string testName, ApiGatewayResponseTestCase testCase)
        {
            await RetryHelper.RetryOperation(async () =>
            {
                await RunV1Test(testCase, _fixture.RestApiUrl, ApiGatewayEmulatorMode.Rest);
                return true;
            });
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task IntegrationTest_APIGatewayV1_HTTP(string testName, ApiGatewayResponseTestCase testCase)
        {
            await RetryHelper.RetryOperation(async () =>
            {
                await RunV1Test(testCase, _fixture.HttpApiV1Url, ApiGatewayEmulatorMode.HttpV1);
                return true;
            });
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V2TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public async Task IntegrationTest_APIGatewayV2(string testName, ApiGatewayResponseTestCase testCase)
        {
            await RetryHelper.RetryOperation(async () =>
            {
                var testResponse = testCase.Response as APIGatewayHttpApiV2ProxyResponse;
                Assert.NotNull(testResponse);
                var (actualResponse, httpTestResponse) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(testResponse, _fixture.HttpApiV2Url);
                await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpTestResponse);
                await testCase.IntegrationAssertions(actualResponse, ApiGatewayEmulatorMode.HttpV2);
                return true;
            });
        }

        private async Task RunV1Test(ApiGatewayResponseTestCase testCase, string apiUrl, ApiGatewayEmulatorMode emulatorMode)
        {
            var testResponse = testCase.Response as APIGatewayProxyResponse;
            Assert.NotNull(testResponse);
            var (actualResponse, httpTestResponse) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(testResponse, apiUrl, emulatorMode);
            await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpTestResponse);
            await testCase.IntegrationAssertions(actualResponse, emulatorMode);
        }
    }
}
