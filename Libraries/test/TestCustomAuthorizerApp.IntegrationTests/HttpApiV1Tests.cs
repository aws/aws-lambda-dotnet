using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for HTTP API v1 (payload format 1.0) endpoints with custom authorizer.
/// HTTP API v1 uses the same request structure as REST API.
/// </summary>
[Collection("Integration Tests")]
public class HttpApiV1Tests
{
    private readonly IntegrationTestContextFixture _fixture;

    public HttpApiV1Tests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

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

    [Fact]
    public async Task HttpApiV1UserInfo_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange & Act
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.HttpApiUrl}/api/http-v1-user-info");

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403 but got {response.StatusCode}");
    }

    [Fact]
    public async Task HttpApiV1UserInfo_WithInvalidToken_ReturnsForbidden()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/http-v1-user-info");
        // Send an invalid token - authorizer uses allow-list approach
        request.Headers.TryAddWithoutValidation("Authorization", "invalid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403 but got {response.StatusCode}");
    }
}
