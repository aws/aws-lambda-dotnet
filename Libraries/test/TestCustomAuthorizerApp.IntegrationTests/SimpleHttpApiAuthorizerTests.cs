using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for endpoints protected by the IAuthorizerResult-based HTTP API authorizer.
/// These verify the end-to-end flow: IAuthorizerResult.Serialize() produces the correct
/// simple response JSON → API Gateway accepts it → protected endpoint receives context values.
/// 
/// The authorizer under test is <see cref="TestCustomAuthorizerApp.AuthorizerFunction.SimpleHttpApiAuthorize"/>
/// which returns IAuthorizerResult (AuthorizerResults.Allow()/Deny()) instead of raw API Gateway types.
/// </summary>
[Collection("Integration Tests")]
public class SimpleHttpApiAuthorizerTests
{
    private readonly IntegrationTestContextFixture _fixture;

    public SimpleHttpApiAuthorizerTests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Tests that an IAuthorizerResult-based HTTP API authorizer correctly allows requests
    /// and passes context values to the protected endpoint.
    /// 
    /// Flow: Request with valid token → IAuthorizerResult authorizer calls AuthorizerResults.Allow().WithContext(...)
    /// → generated handler serializes to simple response format → API Gateway accepts →
    /// protected endpoint extracts context via [FromCustomAuthorizer] → returns values
    /// </summary>
    [Fact]
    public async Task SimpleHttpApiUserInfo_WithValidAuth_ReturnsAuthorizerContext()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/simple-httpapi-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify FromCustomAuthorizer extracted the values set by AuthorizerResults.Allow().WithContext(...)
        Assert.Equal("user-12345", json["UserId"]?.ToString());
        Assert.Equal("test@example.com", json["Email"]?.ToString());
        Assert.Equal("42", json["TenantId"]?.ToString());
        Assert.Equal("Simple HTTP API (IAuthorizerResult)", json["ApiType"]?.ToString());
    }

    /// <summary>
    /// Tests that an IAuthorizerResult-based HTTP API authorizer correctly denies requests
    /// when AuthorizerResults.Deny() is returned.
    /// 
    /// Flow: Request with invalid token → IAuthorizerResult authorizer calls AuthorizerResults.Deny()
    /// → generated handler serializes { isAuthorized: false } → API Gateway returns 403
    /// </summary>
    [Fact]
    public async Task SimpleHttpApiUserInfo_WithInvalidAuth_Returns403()
    {
        // Arrange - use an invalid token that the authorizer will deny
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/simple-httpapi-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - API Gateway returns 403 when the authorizer denies the request
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that the IAuthorizerResult-based authorizer denies requests with no authorization header.
    /// </summary>
    [Fact]
    public async Task SimpleHttpApiUserInfo_WithNoAuth_Returns403()
    {
        // Arrange - no authorization header
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/simple-httpapi-user-info");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - API Gateway returns 401 or 403 when no auth is provided
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403, but got {(int)response.StatusCode} {response.StatusCode}");
    }
}
