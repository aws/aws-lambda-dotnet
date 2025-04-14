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
            var (actualResponse, httpTestResponse) = await _helper.ExecuteTestRequest(testResponse, testName);
            await _helper.AssertResponsesEqual(actualResponse, httpTestResponse);
            await testCase.IntegrationAssertions(actualResponse, ApiGatewayEmulatorMode.HttpV2);
        }

        private async Task RunV1Test(ApiGatewayResponseTestCase testCase, ApiGatewayEmulatorMode emulatorMode, string testName)
        {
            var testResponse = testCase.Response as APIGatewayProxyResponse;
            Assert.NotNull(testResponse);
            var testCaseName = testName + emulatorMode;
            var (actualResponse, httpTestResponse) = await _helper.ExecuteTestRequest(testResponse, emulatorMode, testCaseName);
            await _helper.AssertResponsesEqual(actualResponse, httpTestResponse);
            await testCase.IntegrationAssertions(actualResponse, emulatorMode);
        }
    }
}
