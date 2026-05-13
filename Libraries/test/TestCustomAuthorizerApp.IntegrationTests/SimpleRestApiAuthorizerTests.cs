using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for endpoints protected by the IAuthorizerResult-based REST API authorizer.
/// These verify the end-to-end flow: IAuthorizerResult.Serialize() produces the correct
/// IAM policy JSON → API Gateway accepts it → protected endpoint receives context values.
/// 
/// The authorizer under test is <see cref="TestCustomAuthorizerApp.AuthorizerFunction.SimpleRestApiAuthorize"/>
/// which returns IAuthorizerResult (AuthorizerResults.Allow()/Deny()) instead of raw API Gateway types.
/// The generated handler serializes this to an IAM policy document with the correct MethodArn.
/// </summary>
[Collection("Integration Tests")]
public class SimpleRestApiAuthorizerTests
{
    private readonly IntegrationTestContextFixture _fixture;

    public SimpleRestApiAuthorizerTests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Tests that an IAuthorizerResult-based REST API authorizer correctly allows requests
    /// and passes context values to the protected endpoint.
    /// 
    /// Flow: Request with valid token → IAuthorizerResult authorizer calls 
    /// AuthorizerResults.Allow().WithPrincipalId(...).WithContext(...)
    /// → generated handler serializes to IAM policy with Allow effect → API Gateway accepts →
    /// protected endpoint extracts context via [FromCustomAuthorizer] → returns values
    /// </summary>
    [Fact]
    public async Task SimpleRestApiUserInfo_WithValidAuth_ReturnsAuthorizerContext()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.RestApiUrl}/api/simple-restapi-user-info");
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
        Assert.Equal("Simple REST API (IAuthorizerResult)", json["ApiType"]?.ToString());
    }

    /// <summary>
    /// Tests that an IAuthorizerResult-based REST API authorizer correctly denies requests
    /// when AuthorizerResults.Deny() is returned.
    /// 
    /// Flow: Request with invalid token → IAuthorizerResult authorizer calls AuthorizerResults.Deny()
    /// → generated handler serializes IAM policy with Deny effect → API Gateway returns 403
    /// </summary>
    [Fact]
    public async Task SimpleRestApiUserInfo_WithInvalidAuth_Returns403()
    {
        // Arrange - use an invalid token that the authorizer will deny
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.RestApiUrl}/api/simple-restapi-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - API Gateway returns 403 when the authorizer denies via IAM policy
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that the IAuthorizerResult-based REST API authorizer denies requests with no authorization header.
    /// REST API token authorizers return 401 when the identity source (Authorization header) is missing.
    /// </summary>
    [Fact]
    public async Task SimpleRestApiUserInfo_WithNoAuth_ReturnsUnauthorized()
    {
        // Arrange - no authorization header
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.RestApiUrl}/api/simple-restapi-user-info");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - REST API returns 401 when identity source is missing
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 401 or 403, but got {(int)response.StatusCode} {response.StatusCode}");
    }
}
