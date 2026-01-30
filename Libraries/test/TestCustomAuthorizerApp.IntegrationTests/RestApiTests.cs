using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for REST API (API Gateway v1) endpoints with custom authorizer.
/// REST API uses a different authorizer context structure than HTTP API v2.
/// </summary>
[Collection("Integration Tests")]
public class RestApiTests
{
    private readonly IntegrationTestContextFixture _fixture;

    public RestApiTests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

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

    [Fact]
    public async Task RestUserInfo_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange & Act
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.RestApiUrl}/api/rest-user-info");

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403 but got {response.StatusCode}");
    }

    [Fact]
    public async Task RestUserInfo_WithInvalidToken_ReturnsForbidden()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.RestApiUrl}/api/rest-user-info");
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
