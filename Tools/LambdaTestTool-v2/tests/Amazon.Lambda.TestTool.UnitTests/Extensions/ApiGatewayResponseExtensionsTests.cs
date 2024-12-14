// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using static ApiGatewayResponseTestCases;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions
{
    public class ApiGatewayResponseExtensionsUnitTests
    {
        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public void ToHttpResponse_ConvertsCorrectlyV1(string testName, ApiGatewayResponseTestCase testCase)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            ((APIGatewayProxyResponse)testCase.Response).ToHttpResponse(httpContext, ApiGatewayEmulatorMode.HttpV1);

            // Assert
            testCase.Assertions(httpContext.Response, ApiGatewayEmulatorMode.HttpV1);
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public void ToHttpResponse_ConvertsCorrectlyV1Rest(string testName, ApiGatewayResponseTestCase testCase)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            ((APIGatewayProxyResponse)testCase.Response).ToHttpResponse(httpContext, ApiGatewayEmulatorMode.Rest);

            // Assert
            testCase.Assertions(httpContext.Response, ApiGatewayEmulatorMode.Rest);
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V2TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters")]
        public void ToHttpResponse_ConvertsCorrectlyV2(string testName, ApiGatewayResponseTestCase testCase)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            ((APIGatewayHttpApiV2ProxyResponse)testCase.Response).ToHttpResponse(httpContext);

            // Assert
            testCase.Assertions(httpContext.Response, ApiGatewayEmulatorMode.HttpV2);
        }

        [Fact]
        public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_InfersResponseFormatWhenStatusCodeNotSet()
        {
            var jsonBody = "{\"key\":\"value\"}";
            var apiResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = jsonBody,
                StatusCode = 0 // No status code set
            };

            var httpContext = new DefaultHttpContext();
            apiResponse.ToHttpResponse(httpContext);

            Assert.Equal(200, httpContext.Response.StatusCode);
            Assert.Equal("application/json", httpContext.Response.ContentType);

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpContext.Response.Body);
            var bodyContent = reader.ReadToEnd();
            Assert.Equal(jsonBody, bodyContent);
        }
    }
}
