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
        public async Task IntegrationTest_APIGatewayV1_REST(string testName, ApiGatewayResponseTestCase testCase)
        {
            var uniqueRoute = TestUtils.GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToRestApi(
                _fixture.BaseRestApiId,
                _fixture.ParseAndReturnBodyLambdaFunctionArn,
                uniqueRoute);

            try
            {
                await RetryHelper.RetryOperation(async () =>
                {
                    await RunV1Test(testCase, _fixture.BaseRestApiUrl + uniqueRoute, ApiGatewayEmulatorMode.Rest);
                    return true;
                });
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromRestApi(_fixture.BaseRestApiId, routeId);
            }
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public async Task IntegrationTest_APIGatewayV1_HTTP(string testName, ApiGatewayResponseTestCase testCase)
        {
            var uniqueRoute = TestUtils.GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToHttpApi(
                _fixture.BaseHttpApiV1Id,
                _fixture.ParseAndReturnBodyLambdaFunctionArn,
                "1.0",
                uniqueRoute,
                "POST");

            try
            {
                await RetryHelper.RetryOperation(async () =>
                {
                    await RunV1Test(testCase, _fixture.BaseHttpApiV1Url + uniqueRoute, ApiGatewayEmulatorMode.HttpV1);
                    return true;
                });
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromHttpApi(_fixture.BaseHttpApiV1Id, routeId);
            }
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V2TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public async Task IntegrationTest_APIGatewayV2(string testName, ApiGatewayResponseTestCase testCase)
        {
            var uniqueRoute = TestUtils.GetUniqueRoutePath();
            var routeId = await _fixture.ApiGatewayHelper.AddRouteToHttpApi(
                _fixture.BaseHttpApiV2Id,
                _fixture.ParseAndReturnBodyLambdaFunctionArn,
                "2.0",
                uniqueRoute,
                "POST");

            try
            {
                await RetryHelper.RetryOperation(async () =>
                {
                    var testResponse = testCase.Response as APIGatewayHttpApiV2ProxyResponse;
                    Assert.NotNull(testResponse);
                    var (actualResponse, httpTestResponse) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(
                        testResponse, 
                        _fixture.BaseHttpApiV2Url + uniqueRoute);
                    await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpTestResponse);
                    await testCase.IntegrationAssertions(actualResponse, ApiGatewayEmulatorMode.HttpV2);
                    return true;
                });
            }
            finally
            {
                await _fixture.ApiGatewayHelper.DeleteRouteFromHttpApi(_fixture.BaseHttpApiV2Id, routeId);
            }
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
