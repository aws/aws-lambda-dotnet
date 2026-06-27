using System.Net;
using Xunit;
using Xunit.Extensions.AssemblyFixture;

namespace TestCustomAuthorizerApp.IntegrationTests;

/// <summary>
/// Tests for the health check endpoint which does not require authorization.
/// </summary>
public class HealthCheckTests : IAssemblyFixture<IntegrationTestContextFixture>
{
    private readonly IntegrationTestContextFixture _fixture;

    public HealthCheckTests(IntegrationTestContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk_WithoutAuthorization()
    {
        // Arrange & Act
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.HttpApiUrl}/api/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("OK", content);
    }
}
