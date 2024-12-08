// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Extensions;
using static Amazon.Lambda.TestTool.UnitTests.Extensions.HttpContextTestCases;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions
{
    public class HttpContextExtensionsTests
    {
        [Theory]
        [MemberData(nameof(HttpContextTestCases.V1TestCases), MemberType = typeof(HttpContextTestCases))]
        public void ToApiGatewayRequest_ConvertsCorrectly(string testName, ApiGatewayTestCaseForRequest testCase)
        {
            // Arrange
            var context = testCase.HttpContext;

            // Act
            var result = context.ToApiGatewayRequest(testCase.ApiGatewayRouteConfig);

            // Assert
            testCase.Assertions(result);
        }

        [Theory]
        [MemberData(nameof(HttpContextTestCases.V2TestCases), MemberType = typeof(HttpContextTestCases))]
        public void ToApiGatewayHttpV2Request_ConvertsCorrectly(string testName, ApiGatewayTestCaseForRequest testCase)
        {
            // Arrange
            var context = testCase.HttpContext;

            // Act
            var result = context.ToApiGatewayHttpV2Request(testCase.ApiGatewayRouteConfig);

            // Assert
            testCase.Assertions(result);
        }
    }
}
