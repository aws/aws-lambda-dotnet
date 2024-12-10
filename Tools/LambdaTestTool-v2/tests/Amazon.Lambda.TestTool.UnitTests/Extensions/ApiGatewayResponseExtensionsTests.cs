using System;
using System.IO;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using Xunit;
using static ApiGatewayResponseTestCases;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions
{
    public class ApiGatewayResponseExtensionsUnitTests
    {
        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public void ToHttpResponse_ConvertsCorrectlyV1(string testName, ApiGatewayResponseTestCase testCase)
        {
            // Arrange
            HttpResponse httpResponse = ((APIGatewayProxyResponse)testCase.Response).ToHttpResponse(ApiGatewayEmulatorMode.HttpV1);

            // Assert
            testCase.Assertions(httpResponse, ApiGatewayEmulatorMode.HttpV1);
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V1TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public void ToHttpResponse_ConvertsCorrectlyV1Rest(string testName, ApiGatewayResponseTestCase testCase)
        {
            // Arrange
            HttpResponse httpResponse = ((APIGatewayProxyResponse)testCase.Response).ToHttpResponse(ApiGatewayEmulatorMode.Rest);

            // Assert
            testCase.Assertions(httpResponse, ApiGatewayEmulatorMode.Rest);
        }

        [Theory]
        [MemberData(nameof(ApiGatewayResponseTestCases.V2TestCases), MemberType = typeof(ApiGatewayResponseTestCases))]
        public void ToHttpResponse_ConvertsCorrectlyV2(string testName, ApiGatewayResponseTestCase testCase)
        {
            // Arrange
            HttpResponse httpResponse = ((APIGatewayHttpApiV2ProxyResponse)testCase.Response).ToHttpResponse();

            // Assert
            testCase.Assertions(httpResponse, ApiGatewayEmulatorMode.HttpV2);
        }

        [Fact]
        public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_InfersResponseFormatForValidJson()
        {
            var jsonBody = "{\"key\":\"value\"}";
            var apiResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = jsonBody,
                StatusCode = 0 // No status code set
            };

            var httpResponse = apiResponse.ToHttpResponse();

            Assert.Equal(200, httpResponse.StatusCode);
            Assert.Equal("application/json", httpResponse.ContentType);

            httpResponse.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpResponse.Body);
            var bodyContent = reader.ReadToEnd();
            Assert.Equal(jsonBody, bodyContent);
        }

        [Fact]
        public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_InfersResponseFormatForValidJson2()
        {
            var jsonBody = "hello lambda";
            var apiResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = jsonBody,
                StatusCode = 0 // No status code set
            };

            var httpResponse = apiResponse.ToHttpResponse();

            Assert.Equal(200, httpResponse.StatusCode);
            Assert.Equal("application/json", httpResponse.ContentType);

            httpResponse.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpResponse.Body);
            var bodyContent = reader.ReadToEnd();
            Assert.Equal(jsonBody, bodyContent);
        }

        [Fact]
        public void ToHttpResponse_APIGatewayHttpApiV2ProxyResponse_HandlesNonJsonResponse()
        {
            var apiResponse = new APIGatewayHttpApiV2ProxyResponse
            {
                Body = "{this is not valid}",
                StatusCode = 0 // No status code set
            };

            var httpResponse = apiResponse.ToHttpResponse();

            Assert.Equal(500, httpResponse.StatusCode);
            Assert.Equal("application/json", httpResponse.ContentType);

            httpResponse.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpResponse.Body);
            var bodyContent = reader.ReadToEnd();
            Assert.Equal("{\"message\":\"Internal Server Error\"}", bodyContent);
            Assert.Equal(35, httpResponse.ContentLength);
        }
        
    }
}
