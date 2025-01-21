// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Models;
using System.Text;
using System.Text.Json;

namespace Amazon.Lambda.TestTool.IntegrationTests;

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
[Collection("ApiGateway Integration Tests")]
public class InvokeResponseExtensionsIntegrationTests
{
    private readonly ApiGatewayIntegrationTestFixture _fixture;

    public InvokeResponseExtensionsIntegrationTests(ApiGatewayIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

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
        var convertedResponse = invokeResponse.ToApiGatewayProxyResponse(emulatorMode);

        // Assert
        var apiUrl = emulatorMode == ApiGatewayEmulatorMode.Rest
            ? _fixture.ParseAndReturnBodyRestApiUrl
            : _fixture.ParseAndReturnBodyHttpApiV1Url;
        var (actualResponse, httpTestResponse) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(convertedResponse, apiUrl, emulatorMode);
        await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpTestResponse);
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
        var (actualResponse, httpTestResponse) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(convertedResponse, _fixture.ParseAndReturnBodyHttpApiV2Url);
        await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpTestResponse);
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

        // Assert
        Assert.Equal(expectedStatusCode, convertedResponse.StatusCode);
        Assert.Contains(expectedErrorMessage, convertedResponse.Body);

        var apiUrl = emulatorMode == ApiGatewayEmulatorMode.Rest
            ? _fixture.ParseAndReturnBodyRestApiUrl
            : _fixture.ParseAndReturnBodyHttpApiV1Url;
        var (actualResponse, _) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(convertedResponse, apiUrl, emulatorMode);
        Assert.Equal(expectedStatusCode, (int)actualResponse.StatusCode);
        var content = await actualResponse.Content.ReadAsStringAsync();
        Assert.Contains(expectedErrorMessage, content);
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
    [InlineData("{\"name\": \"John Doe\", \"age\":", "{\"name\": \"John Doe\", \"age\":")]  // Invalid JSON (partial object)
    [InlineData("{\"name\": \"John Doe\", \"age\": 30}", "{\"name\": \"John Doe\", \"age\": 30}")]  // Valid JSON object without statusCode
    [InlineData("[1, 2, 3, 4, 5]", "[1, 2, 3, 4, 5]")]  // JSON array
    [InlineData("Hello, World!", "Hello, World!")]  // String primitive
    [InlineData("42", "42")]  // Number primitive
    [InlineData("true", "true")]  // Boolean primitive
    [InlineData("\"test\"", "test")]  // JSON string that should be unescaped
    [InlineData("\"Hello, World!\"", "Hello, World!")]  // JSON string with spaces
    [InlineData("\"\"", "")]  // Empty JSON string
    [InlineData("\"Special \\\"quoted\\\" text\"", "Special \"quoted\" text")]  // JSON string with escaped quotes
    public async Task ToApiGatewayHttpApiV2ProxyResponse_VariousPayloads_ReturnsAsRawBody(string inputPayload, string expectedResponsePayload)
    {
        // Arrange
        var invokeResponse = new InvokeResponse
        {
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(inputPayload))
        };

        // Act
        var actualConvertedResponse = invokeResponse.ToApiGatewayHttpApiV2ProxyResponse();

        // Assert
        Assert.Equal(200, actualConvertedResponse.StatusCode);
        Assert.Equal(expectedResponsePayload, actualConvertedResponse.Body);
        Assert.Equal("application/json", actualConvertedResponse.Headers["Content-Type"]);

        // Verify against actual API Gateway behavior
        var (actualResponse, httpTestResponse) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(actualConvertedResponse, _fixture.ParseAndReturnBodyHttpApiV2Url);
        await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpTestResponse);

        // Additional checks for API Gateway specific behavior
        Assert.Equal(200, (int)actualResponse.StatusCode);
        var actualContent = await actualResponse.Content.ReadAsStringAsync();
        Assert.Equal(expectedResponsePayload, actualContent);
        Assert.Equal("application/json", actualResponse.Content.Headers.ContentType?.ToString());
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

        // Verify against actual API Gateway behavior
        var (actualResponse, httpTestResponse) = await _fixture.ApiGatewayTestHelper.ExecuteTestRequest(convertedResponse, _fixture.ParseAndReturnBodyHttpApiV2Url);
        await _fixture.ApiGatewayTestHelper.AssertResponsesEqual(actualResponse, httpTestResponse);

        // Additional checks for API Gateway specific behavior
        Assert.Equal(500, (int)actualResponse.StatusCode);
        var content = await actualResponse.Content.ReadAsStringAsync();
        Assert.Equal("{\"message\":\"Internal Server Error\"}", content);
        Assert.Equal("application/json", actualResponse.Content.Headers.ContentType?.ToString());
    }
}
