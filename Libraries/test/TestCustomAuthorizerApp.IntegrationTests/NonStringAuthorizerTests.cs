using System.Net;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for [FromCustomAuthorizer] with non-string types (int, bool, double).
/// These tests verify that the source generator correctly converts values from
/// the Lambda authorizer context to the specified parameter types.
/// 
/// These tests exercise the type conversion logic in the .tt template's generated code
/// using Convert.ChangeType() to convert authorizer context values to the parameter types.
/// </summary>
[Collection("Integration Tests")]
public class NonStringAuthorizerTests
{
    private readonly IntegrationTestContextFixture _fixture;

    public NonStringAuthorizerTests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Tests that [FromCustomAuthorizer] correctly extracts AND converts non-string values.
    /// 
    /// Flow: Request with valid token → Authorizer returns context with int/bool/double values →
    /// Generated Lambda handler extracts and converts values using Convert.ChangeType() →
    /// Returns values with correct types, proving type conversion worked
    /// 
    /// This tests the core functionality of the .tt template's type conversion code.
    /// </summary>
    [Fact]
    public async Task NonStringUserInfo_WithValidAuth_ReturnsConvertedValues()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/nonstring-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify the int value was correctly extracted and converted
        Assert.Equal(42, json["TenantId"]?.Value<int>());
        // Verify the int arithmetic works (proving it's actually an int)
        Assert.Equal(43, json["TenantIdPlusOne"]?.Value<int>());

        // Verify the bool value was correctly extracted and converted
        Assert.True(json["IsAdmin"]?.Value<bool>());
        // Verify the conditional logic based on bool works
        Assert.Equal("Administrator", json["AdminStatus"]?.ToString());

        // Verify the double value was correctly extracted and converted
        Assert.Equal(95.5, json["Score"]?.Value<double>());
        // Verify double arithmetic works (proving it's actually a double)
        Assert.Equal(0.955, json["ScorePercentage"]!.Value<double>(), 3);

        // Verify success message
        Assert.Contains("Successfully extracted non-string types", json["Message"]?.ToString());
    }

    /// <summary>
    /// Tests that the generated Lambda handler returns 401 when expected authorizer context keys are missing.
    /// 
    /// Flow: Request with partial-context token → Authorizer returns IsAuthorized=true but 
    /// WITHOUT expected context keys → Generated Lambda handler checks for required keys →
    /// Returns 401 Unauthorized because expected [FromCustomAuthorizer] values are missing
    /// 
    /// This tests the defensive 401 handling in the .tt template for non-string type parameters.
    /// </summary>
    [Fact]
    public async Task NonStringUserInfo_WithMissingAuthorizerContextKey_ReturnsUnauthorized()
    {
        // Arrange - use partial-context token that authorizes but omits expected context keys
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/nonstring-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "partial-context");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert - generated Lambda handler returns 401 when context key is missing
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies that int values from authorizer context are properly typed in the JSON response.
    /// </summary>
    [Fact]
    public async Task NonStringUserInfo_IntValueIsCorrectType()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/nonstring-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify TenantId is returned as a number, not a string
        var tenantIdToken = json["TenantId"];
        Assert.NotNull(tenantIdToken);
        Assert.Equal(JTokenType.Integer, tenantIdToken.Type);
    }

    /// <summary>
    /// Verifies that bool values from authorizer context are properly typed in the JSON response.
    /// </summary>
    [Fact]
    public async Task NonStringUserInfo_BoolValueIsCorrectType()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/nonstring-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify IsAdmin is returned as a boolean, not a string
        var isAdminToken = json["IsAdmin"];
        Assert.NotNull(isAdminToken);
        Assert.Equal(JTokenType.Boolean, isAdminToken.Type);
    }

    /// <summary>
    /// Verifies that double values from authorizer context are properly typed in the JSON response.
    /// </summary>
    [Fact]
    public async Task NonStringUserInfo_DoubleValueIsCorrectType()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_fixture.HttpApiUrl}/api/nonstring-user-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");

        // Act
        var response = await _fixture.HttpClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        // Verify Score is returned as a float, not a string
        var scoreToken = json["Score"];
        Assert.NotNull(scoreToken);
        Assert.Equal(JTokenType.Float, scoreToken.Type);
    }
}
