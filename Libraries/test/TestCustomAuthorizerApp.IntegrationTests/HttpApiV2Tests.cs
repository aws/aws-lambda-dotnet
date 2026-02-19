using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for HTTP API v2 (payload format 2.0) endpoints with custom authorizer.
/// HTTP API v2 uses APIGatewayHttpApiV2ProxyRequest with RequestContext.Authorizer.Lambda for context.
/// 
/// These tests verify that the source-generated Lambda handler correctly extracts
/// values from the authorizer context using [FromCustomAuthorizer] attributes.
/// </summary>
[Collection("Integration Tests")]
public class HttpApiV2Tests
{
    private readonly IntegrationTestContextFixture _fixture;

    public HttpApiV2Tests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Tests basic protected endpoint access with valid authorization.
    /// 
    /// Flow: Request with valid token → Authorizer returns IsAuthorized=true with context →
    /// Generated Lambda handler extracts context values → Returns success with extracted values
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        // The endpoint returns context info - should contain the authorizer values
        Assert.Contains("user-12345", content);
    }

    /// <summary>
    /// Tests that [FromCustomAuthorizer] correctly extracts values from the authorizer context.
    /// 
    /// Flow: Request with valid token → Authorizer returns IsAuthorized=true with context →
    /// Generated Lambda handler extracts context values → Returns extracted values
    /// 
    /// This tests the core functionality of the .tt template's context extraction code for HTTP API v2.
    /// </summary>
    [Fact]
    public async Task UserInfo_WithValidAuth_ReturnsAuthorizerContext()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify FromCustomAuthorizer extracted the values correctly
        Assert.Equal("user-12345", json["UserId"]?.ToString());
        Assert.Equal("test@example.com", json["Email"]?.ToString());
        Assert.Equal("42", json["TenantId"]?.ToString());
    }

    /// <summary>
    /// Tests that the generated Lambda handler returns 401 when expected authorizer context keys are missing.
    /// </summary>
    [Fact]
    public async Task UserInfo_WithMissingAuthorizerContextKey_ReturnsUnauthorized()
    {
        // Arrange - use partial-context token that authorizes but omits expected context keys
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "partial-context");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - generated Lambda handler returns 401 when context key is missing
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests IHttpResult return type with [FromCustomAuthorizer] context extraction.
    /// </summary>
    [Fact]
    public async Task IHttpResult_WithValidAuth_ReturnsSuccess()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/ihttpresult-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify FromCustomAuthorizer extracted the values correctly
        Assert.Equal("user-12345", json["UserId"]?.ToString());
        Assert.Equal("test@example.com", json["Email"]?.ToString());
    }

    /// <summary>
    /// Tests that the generated Lambda handler returns 401 when expected authorizer context keys are missing
    /// for endpoints returning IHttpResult.
    /// </summary>
    [Fact]
    public async Task IHttpResult_WithMissingAuthorizerContextKey_ReturnsUnauthorized()
    {
        // Arrange - use partial-context token that authorizes but omits expected context keys
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/ihttpresult-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "partial-context");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - generated Lambda handler returns 401 when context key is missing
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
