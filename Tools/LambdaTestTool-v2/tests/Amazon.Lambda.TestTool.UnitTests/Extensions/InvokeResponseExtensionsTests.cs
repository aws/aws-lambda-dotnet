// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions;

public class InvokeResponseExtensionsTests
{
    [Theory]
    [InlineData("{\"statusCode\": 200, \"body\": \"Hello\", \"headers\": {\"Content-Type\": \"text/plain\"}}", ApiGatewayEmulatorMode.Rest, 200, "Hello", "text/plain")]
    [InlineData("{\"statusCode\": 201, \"body\": \"Created\", \"headers\": {\"Content-Type\": \"application/json\"}}", ApiGatewayEmulatorMode.HttpV1, 201, "Created", "application/json")]
    public void ToApiGatewayProxyResponse_ValidFullResponse_ReturnsCorrectly(string payload, ApiGatewayEmulatorMode mode, int expectedStatusCode, string expectedBody, string expectedContentType)
    {
        var invokeResponse = CreateInvokeResponse(payload);
        var result = invokeResponse.ToApiGatewayProxyResponse(mode);

        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.Equal(expectedBody, result.Body);
        Assert.Equal(expectedContentType, result.Headers["Content-Type"]);
    }

    [Theory]
    [InlineData("{invalid json}", ApiGatewayEmulatorMode.Rest, 502, "{\"message\":\"Internal server error\"}")]
    [InlineData("{invalid json}", ApiGatewayEmulatorMode.HttpV1, 500, "{\"message\":\"Internal Server Error\"}")]
    [InlineData("", ApiGatewayEmulatorMode.Rest, 502, "{\"message\":\"Internal server error\"}")]
    public void ToApiGatewayProxyResponse_InvalidOrEmptyJson_ReturnsErrorResponse(string payload, ApiGatewayEmulatorMode mode, int expectedStatusCode, string expectedBody)
    {
        var invokeResponse = CreateInvokeResponse(payload);
        var result = invokeResponse.ToApiGatewayProxyResponse(mode);

        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.Equal(expectedBody, result.Body);
        Assert.Equal("application/json", result.Headers["Content-Type"]);
    }

    [Theory]
    [InlineData("{\"statusCode\": 200, \"body\": \"Hello\", \"headers\": {\"Content-Type\": \"text/plain\"}}", 200, "Hello", "text/plain")]
    [InlineData("{\"statusCode\": \"invalid\", \"body\": \"Hello\"}", 500, "{\"message\":\"Internal Server Error\"}", "application/json")]
    [InlineData("{\"message\": \"Hello, World!\"}", 200, "{\"message\": \"Hello, World!\"}", "application/json")]
    [InlineData("test", 200, "test", "application/json")]
    [InlineData("\"test\"", 200, "test", "application/json")]
    [InlineData("42", 200, "42", "application/json")]
    [InlineData("true", 200, "true", "application/json")]
    [InlineData("[1,2,3]", 200, "[1,2,3]", "application/json")]
    [InlineData("{invalid json}", 200, "{invalid json}", "application/json")]
    [InlineData("", 200, "", "application/json")]
    public void ToApiGatewayHttpApiV2ProxyResponse_VariousInputs_ReturnsExpectedResult(string payload, int expectedStatusCode, string expectedBody, string expectedContentType)
    {
        var invokeResponse = CreateInvokeResponse(payload);
        var result = invokeResponse.ToApiGatewayHttpApiV2ProxyResponse();

        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.Equal(expectedBody, result.Body);
        Assert.Equal(expectedContentType, result.Headers["Content-Type"]);
    }

    [Fact]
    public void ToApiGatewayProxyResponse_UnsupportedEmulatorMode_ThrowsNotSupportedException()
    {
        var invokeResponse = CreateInvokeResponse("{\"statusCode\": 200, \"body\": \"Hello\"}");

        Assert.Throws<NotSupportedException>(() =>
            invokeResponse.ToApiGatewayProxyResponse(ApiGatewayEmulatorMode.HttpV2));
    }

    [Fact]
    public void ToApiGatewayHttpApiV2ProxyResponse_StatusCodeAsFloat_ReturnsInternalServerError()
    {
        // Arrange
        var payload = "{\"statusCode\": 200.5, \"body\": \"Hello\", \"headers\": {\"Content-Type\": \"text/plain\"}}";
        var invokeResponse = CreateInvokeResponse(payload);

        // Act
        var result = invokeResponse.ToApiGatewayHttpApiV2ProxyResponse();

        // Assert
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("{\"message\":\"Internal Server Error\"}", result.Body);
        Assert.Equal("application/json", result.Headers["Content-Type"]);
    }


    private InvokeResponse CreateInvokeResponse(string payload)
    {
        return new InvokeResponse
        {
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(payload))
        };
    }
}
