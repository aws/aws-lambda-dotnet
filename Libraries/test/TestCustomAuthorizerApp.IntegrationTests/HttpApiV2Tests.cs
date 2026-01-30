using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for HTTP API v2 (payload format 2.0) endpoints with custom authorizer.
/// </summary>
[Collection("Integration Tests")]
public class HttpApiV2Tests
{
    private readonly IntegrationTestContextFixture _fixture;

    public HttpApiV2Tests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

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

    [Fact]
    public async Task ProtectedEndpoint_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange & Act
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.HttpApiUrl}/api/protected");

        // Assert
        // HTTP API with Lambda authorizer returns 401 when authorization header is missing
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403 but got {response.StatusCode}");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_ReturnsForbidden()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/protected");
        // Send an invalid token - authorizer uses allow-list approach
        request.Headers.TryAddWithoutValidation("Authorization", "invalid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403 but got {response.StatusCode}");
    }

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

    [Fact]
    public async Task UserInfo_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange & Act
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.HttpApiUrl}/api/user-info");

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403 but got {response.StatusCode}");
    }

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

    [Fact]
    public async Task IHttpResult_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange & Act
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.HttpApiUrl}/api/ihttpresult-user-info");

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403 but got {response.StatusCode}");
    }
}
