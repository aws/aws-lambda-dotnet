using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for HTTP API v1 (payload format 1.0) endpoints with custom authorizer.
/// HTTP API v1 uses the same request structure as REST API (APIGatewayProxyRequest).
/// 
/// These tests verify that the source-generated Lambda handler correctly extracts
/// values from the authorizer context using [FromCustomAuthorizer] attributes.
/// </summary>
[Collection("Integration Tests")]
public class HttpApiV1Tests
{
    private readonly IntegrationTestContextFixture _fixture;

    public HttpApiV1Tests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Tests that [FromCustomAuthorizer] correctly extracts values from the authorizer context.
    /// 
    /// Flow: Request with valid token → Authorizer returns IsAuthorized=true with context →
    /// Generated Lambda handler extracts context values → Returns extracted values
    /// 
    /// This tests the core functionality of the .tt template's context extraction code.
    /// </summary>
    [Fact]
    public async Task HttpApiV1UserInfo_WithValidAuth_ReturnsAuthorizerContext()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/http-v1-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify FromCustomAuthorizer extracted the values correctly from v1 format
        Assert.Equal("user-12345", json["UserId"]?.ToString());
        Assert.Equal("test@example.com", json["Email"]?.ToString());
        Assert.Equal("42", json["TenantId"]?.ToString());
        Assert.Equal("HTTP API V1", json["ApiType"]?.ToString());
    }

    /// <summary>
    /// Tests that the generated Lambda handler returns 401 when expected authorizer context keys are missing.
    /// 
    /// Flow: Request with partial-context token → Authorizer returns IsAuthorized=true but 
    /// WITHOUT expected context keys → Generated Lambda handler checks for required keys →
    /// Returns 401 Unauthorized because expected [FromCustomAuthorizer] values are missing
    /// 
    /// This tests the defensive 401 handling in the .tt template when the authorizer
    /// passes the request but doesn't provide all the context values the Lambda expects.
    /// </summary>
    [Fact]
    public async Task HttpApiV1UserInfo_WithMissingAuthorizerContextKey_ReturnsUnauthorized()
    {
        // Arrange - use partial-context token that authorizes but omits expected context keys
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/http-v1-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "partial-context");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - generated Lambda handler returns 401 when context key is missing
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
