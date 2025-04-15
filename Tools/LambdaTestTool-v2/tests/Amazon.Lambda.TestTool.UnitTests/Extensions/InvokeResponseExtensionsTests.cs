// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.Extensions;

/// <summary>
/// Integration tests for InvokeResponseExtensions.
/// </summary>
/// <remarks>
/// Developer's Note:
/// These tests don't have direct access to the intermediate result of the Lambda to API Gateway conversion.
/// Instead, we test the final API Gateway response object to ensure our conversion methods produce results
/// that match the actual API Gateway behavior. This approach allows us to verify the correctness of our
/// conversion methods within the constraints of not having access to AWS's internal conversion process.
/// </remarks>
public class InvokeResponseExtensionsTests
{

    private readonly ApiGatewayTestHelper _helper = new();

    [Theory]
    [InlineData(ApiGatewayEmulatorMode.Rest)]
    [InlineData(ApiGatewayEmulatorMode.HttpV1)]
    public async Task ToApiGatewayProxyResponse_ValidResponse_MatchesDirectConversion(ApiGatewayEmulatorMode emulatorMode)
    {
        // Arrange
        var testResponse = new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new { message = "Hello, World!" }),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
        var invokeResponse = new InvokeResponse
        {
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testResponse)))
        };

        // Act
        var apiGatewayProxyResponse = invokeResponse.ToApiGatewayProxyResponse(emulatorMode);

        var testName = nameof(ToApiGatewayProxyResponse_ValidResponse_MatchesDirectConversion) + emulatorMode;

        // Assert
        await _helper.VerifyApiGatewayResponseAsync(apiGatewayProxyResponse, emulatorMode, testName);
    }

    [Fact]
    public async Task ToApiGatewayHttpApiV2ProxyResponse_ValidResponse_MatchesDirectConversion()
    {
        // Arrange
        var testResponse = new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(new { message = "Hello, World!" }),
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
        var invokeResponse = new InvokeResponse
        {
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testResponse)))
        };

        // Act
        var convertedResponse = invokeResponse.ToApiGatewayHttpApiV2ProxyResponse();

        // Assert
        await _helper.VerifyHttpApiV2ResponseAsync(convertedResponse, nameof(ToApiGatewayHttpApiV2ProxyResponse_ValidResponse_MatchesDirectConversion));
    }

    [Theory]
    [InlineData(ApiGatewayEmulatorMode.Rest, 502, "Internal server error")]
    [InlineData(ApiGatewayEmulatorMode.HttpV1, 500, "Internal Server Error")]
    public async Task ToApiGatewayProxyResponse_InvalidJson_ReturnsErrorResponse(ApiGatewayEmulatorMode emulatorMode, int expectedStatusCode, string expectedErrorMessage)
    {
        // Arrange
        var invokeResponse = new InvokeResponse
        {
            Payload = new MemoryStream(Encoding.UTF8.GetBytes("Not a valid proxy response object"))
        };

        // Act
        var convertedResponse = invokeResponse.ToApiGatewayProxyResponse(emulatorMode);

        var testName = nameof(ToApiGatewayProxyResponse_InvalidJson_ReturnsErrorResponse) + emulatorMode;

        // Assert
        Assert.Equal(expectedStatusCode, convertedResponse.StatusCode);
        Assert.Contains(expectedErrorMessage, convertedResponse.Body);

        await _helper.VerifyApiGatewayResponseAsync(
            convertedResponse,
            emulatorMode,
            testName,
            async httpResponse =>
            {
                Assert.Equal(expectedStatusCode, httpResponse.StatusCode);

                httpResponse.Body.Seek(0, SeekOrigin.Begin);
                var content = await new StreamReader(httpResponse.Body).ReadToEndAsync();
                Assert.Contains(expectedErrorMessage, content);
            });
    }

    /// <summary>
    /// Tests various Lambda return values to verify API Gateway's handling of responses.
    /// </summary>
    /// <param name="expectedResponsePayload">The payload returned by the Lambda function.</param>
    /// <remarks>
    /// This test demonstrates a discrepancy between the official AWS documentation
    /// and the actual observed behavior of API Gateway HTTP API v2 with Lambda
    /// proxy integrations (payload format version 2.0).
    ///
    /// Official documentation states:
    /// "If your Lambda function returns valid JSON and doesn't return a statusCode,
    /// API Gateway assumes a 200 status code and treats the entire response as the body."
    ///
    /// However, the observed behavior (which this test verifies) is:
    /// - API Gateway does not validate whether the returned data is valid JSON.
    /// - Any response from the Lambda function that is not a properly formatted
    ///   API Gateway response object (i.e., an object with a 'statusCode' property)
    ///   is treated as a raw body in a 200 OK response.
    /// - This includes valid JSON objects without a statusCode, JSON arrays,
    ///   primitive values, and invalid JSON strings.
    ///
    /// This test ensures that our ToApiGatewayHttpApiV2ProxyResponse method
    /// correctly replicates this observed behavior, rather than the documented behavior.
    /// </remarks>
    [Theory]
    [InlineData("Invalid_JSON_Partial_Object", "{\"name\": \"John Doe\", \"age\":", "{\"name\": \"John Doe\", \"age\":")]  // Invalid JSON (partial object)
    [InlineData("Valid_JSON_Object", "{\"name\": \"John Doe\", \"age\": 30}", "{\"name\": \"John Doe\", \"age\": 30}")]  // Valid JSON object without statusCode
    [InlineData("JSON_Array", "[1, 2, 3, 4, 5]", "[1, 2, 3, 4, 5]")]  // JSON array
    [InlineData("string", "Hello, World!", "Hello, World!")]  // String primitive
    [InlineData("number", "42", "42")]  // Number primitive
    [InlineData("boolean", "true", "true")]  // Boolean primitive
    [InlineData("string_unescaped", "\"test\"", "test")]  // JSON string that should be unescaped
    [InlineData("string_spaces", "\"Hello, World!\"", "Hello, World!")]  // JSON string with spaces
    [InlineData("empty_string", "\"\"", "")]  // Empty JSON string
    [InlineData("json_special", "\"Special \\\"quoted\\\" text\"", "Special \"quoted\" text")]  // JSON string with escaped quotes
    public async Task ToApiGatewayHttpApiV2ProxyResponse_VariousPayloads_ReturnsAsRawBody(
        string testName,
        string inputPayload,
        string expectedResponsePayload)
    {
        // Arrange
        var invokeResponse = new InvokeResponse
        {
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(inputPayload))
        };

        // Act
        var actualConvertedResponse = invokeResponse.ToApiGatewayHttpApiV2ProxyResponse();

        var testCaseName =  nameof(ToApiGatewayProxyResponse_ValidResponse_MatchesDirectConversion) + testName;

        // Assert
        Assert.Equal(200, actualConvertedResponse.StatusCode);
        Assert.Equal(expectedResponsePayload, actualConvertedResponse.Body);
        Assert.Equal("application/json", actualConvertedResponse.Headers["Content-Type"]);

        await _helper.VerifyHttpApiV2ResponseAsync(
            actualConvertedResponse,
            testCaseName,
            async httpResponse =>
            {
                // Additional checks for API Gateway specific behavior
                Assert.Equal(200, httpResponse.StatusCode);

                httpResponse.Body.Seek(0, SeekOrigin.Begin);
                var content = await new StreamReader(httpResponse.Body).ReadToEndAsync();
                Assert.Equal(expectedResponsePayload, content);

                Assert.Equal("application/json", httpResponse.Headers["Content-Type"]);
            });
    }

    [Fact]
    public async Task ToApiGatewayHttpApiV2ProxyResponse_StatusCodeAsFloat_ReturnsInternalServerError()
    {
        // Arrange
        var responsePayload = "{\"statusCode\": 200.5, \"body\": \"Hello\", \"headers\": {\"Content-Type\": \"text/plain\"}}";
        var invokeResponse = new InvokeResponse
        {
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(responsePayload))
        };

        // Act
        var convertedResponse = invokeResponse.ToApiGatewayHttpApiV2ProxyResponse();

        // Assert
        Assert.Equal(500, convertedResponse.StatusCode);
        Assert.Equal("{\"message\":\"Internal Server Error\"}", convertedResponse.Body);
        Assert.Equal("application/json", convertedResponse.Headers["Content-Type"]);

        await _helper.VerifyHttpApiV2ResponseAsync(
            convertedResponse,
            nameof(ToApiGatewayHttpApiV2ProxyResponse_StatusCodeAsFloat_ReturnsInternalServerError),
            async httpResponse =>
            {
                // Additional checks for API Gateway specific behavior
                Assert.Equal(500, httpResponse.StatusCode);

                httpResponse.Body.Seek(0, SeekOrigin.Begin);
                var content = await new StreamReader(httpResponse.Body).ReadToEndAsync();
                Assert.Equal("{\"message\":\"Internal Server Error\"}", content);

                Assert.Equal("application/json", httpResponse.Headers["Content-Type"]);
            });
    }

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

    [Fact]
    public void ToApiGatewayProxyResponse_UnsupportedEmulatorMode_ThrowsNotSupportedException()
    {
        var invokeResponse = CreateInvokeResponse("{\"statusCode\": 200, \"body\": \"Hello\"}");

        Assert.Throws<NotSupportedException>(() =>
            invokeResponse.ToApiGatewayProxyResponse(ApiGatewayEmulatorMode.HttpV2));
    }

    private InvokeResponse CreateInvokeResponse(string payload)
    {
        return new InvokeResponse
        {
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(payload))
        };
    }
}
