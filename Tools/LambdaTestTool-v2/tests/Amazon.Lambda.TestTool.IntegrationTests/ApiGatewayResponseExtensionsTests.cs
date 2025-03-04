// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.IntegrationTests.Helpers;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Tests.Common;
using Xunit;
using static Amazon.Lambda.TestTool.Tests.Common.ApiGatewayResponseTestCases;

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
                var baseUrl = _fixture.GetAppropriateBaseUrl(TestRoutes.Ids.ParseAndReturnBody, ApiGatewayEmulatorMode.Rest);
                var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.ParseAndReturnBody);
                await RunV1Test(testCase, url, ApiGatewayEmulatorMode.Rest);
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
                var baseUrl = _fixture.GetAppropriateBaseUrl(TestRoutes.Ids.ParseAndReturnBody, ApiGatewayEmulatorMode.HttpV1);
                var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.ParseAndReturnBody);
                await RunV1Test(testCase, url, ApiGatewayEmulatorMode.HttpV1);
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
                var baseUrl = _fixture.GetAppropriateBaseUrl(TestRoutes.Ids.ParseAndReturnBody, ApiGatewayEmulatorMode.HttpV2);
                var url = _fixture.GetRouteUrl(baseUrl, TestRoutes.Ids.ParseAndReturnBody);
                var testResponse = testCase.Response as APIGatewayHttpApiV2ProxyResponse;
                Assert.NotNull(testResponse);
                var (actualResponse, httpTestResponse) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(testResponse, url);
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
