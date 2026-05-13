using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for REST API (API Gateway v1) endpoints with custom authorizer.
/// REST API uses APIGatewayProxyRequest with RequestContext.Authorizer as a dictionary for context.
/// 
/// These tests verify that the source-generated Lambda handler correctly extracts
/// values from the authorizer context using [FromCustomAuthorizer] attributes.
/// </summary>
[Collection("Integration Tests")]
public class RestApiTests
{
    private readonly IntegrationTestContextFixture _fixture;

    public RestApiTests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Tests that [FromCustomAuthorizer] correctly extracts values from the REST API authorizer context.
    /// 
    /// Flow: Request with valid token → Authorizer returns Allow policy with context →
    /// Generated Lambda handler extracts context values from RequestContext.Authorizer →
    /// Returns extracted values
    /// 
    /// This tests the core functionality of the .tt template's context extraction code for REST API.
    /// </summary>
    [Fact]
    public async Task RestUserInfo_WithValidAuth_ReturnsAuthorizerContext()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.RestApiUrl}/api/rest-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify FromCustomAuthorizer extracted the values correctly from REST API format
        Assert.Equal("user-12345", json["UserId"]?.ToString());
        Assert.Equal("test@example.com", json["Email"]?.ToString());
        Assert.Equal("42", json["TenantId"]?.ToString());
        Assert.Equal("REST API", json["ApiType"]?.ToString());
    }

    /// <summary>
    /// Tests that the generated Lambda handler returns 401 when expected authorizer context keys are missing.
    /// 
    /// Flow: Request with partial-context token → Authorizer returns Allow policy but 
    /// WITHOUT expected context keys → Generated Lambda handler checks for required keys →
    /// Returns 401 Unauthorized because expected [FromCustomAuthorizer] values are missing
    /// 
    /// This tests the defensive 401 handling in the .tt template for REST API endpoints.
    /// </summary>
    [Fact]
    public async Task RestUserInfo_WithMissingAuthorizerContextKey_ReturnsUnauthorized()
    {
        // Arrange - use partial-context token that authorizes but omits expected context keys
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.RestApiUrl}/api/rest-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "partial-context");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - generated Lambda handler returns 401 when context key is missing
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
