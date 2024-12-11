using System.Text.Json;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.IO;
using Microsoft.Extensions.Logging;
using Moq;

namespace Amazon.Lambda.TestTool.UnitTests.Services;

public class ApiGatewayRouteConfigServiceTests
{
    private readonly Mock<IEnvironmentManager> _mockEnvironmentManager = new Mock<IEnvironmentManager>();
    private readonly Mock<ILogger<ApiGatewayRouteConfigService>> _mockLogger = new Mock<ILogger<ApiGatewayRouteConfigService>>();

    [Fact]
    public void Constructor_LoadsAndParsesValidEnvironmentVariables()
    {
        // Arrange
        var validConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestLambdaFunction",
            HttpMethod = "GET",
            Path = "/test/{id}"
        };
        var environmentVariables = new Dictionary<string, string>
        {
            { Constants.LambdaConfigEnvironmentVariablePrefix, JsonSerializer.Serialize(validConfig) }
        };

        _mockEnvironmentManager
            .Setup(m => m.GetEnvironmentVariables())
            .Returns(environmentVariables);

        // Act
        var service = new ApiGatewayRouteConfigService(_mockEnvironmentManager.Object, _mockLogger.Object);

        // Assert
        var routeConfig = service.GetRouteConfig("GET", "/test/123");
        Assert.NotNull(routeConfig);
        Assert.Equal("TestLambdaFunction", routeConfig.LambdaResourceName);
    }

    [Fact]
    public void Constructor_IgnoresInvalidEnvironmentVariables()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var environmentVariables = new Dictionary<string, string>
        {
            { Constants.LambdaConfigEnvironmentVariablePrefix, invalidJson }
        };

        _mockEnvironmentManager
            .Setup(m => m.GetEnvironmentVariables())
            .Returns(environmentVariables);

        // Act
        var service = new ApiGatewayRouteConfigService(_mockEnvironmentManager.Object, _mockLogger.Object);

        // Assert
        var routeConfig = service.GetRouteConfig("GET", "/test/123");
        Assert.Null(routeConfig);
    }

    [Fact]
    public void GetRouteConfig_ReturnsNullForNonMatchingHttpMethod()
    {
        // Arrange
        var routeConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestLambdaFunction",
            HttpMethod = "POST",
            Path = "/test/{id}"
        };

        _mockEnvironmentManager
            .Setup(m => m.GetEnvironmentVariables())
            .Returns(new Dictionary<string, string>
            {
                { Constants.LambdaConfigEnvironmentVariablePrefix, JsonSerializer.Serialize(routeConfig) }
            });

        var service = new ApiGatewayRouteConfigService(_mockEnvironmentManager.Object, _mockLogger.Object);

        // Act
        var result = service.GetRouteConfig("GET", "/test/123");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetRouteConfig_ReturnsNullForNonMatchingPath()
    {
        // Arrange
        var routeConfig = new ApiGatewayRouteConfig
        {
            LambdaResourceName = "TestLambdaFunction",
            HttpMethod = "GET",
            Path = "/test/{id}"
        };

        _mockEnvironmentManager
            .Setup(m => m.GetEnvironmentVariables())
            .Returns(new Dictionary<string, string>
            {
                { Constants.LambdaConfigEnvironmentVariablePrefix, JsonSerializer.Serialize(routeConfig) }
            });

        var service = new ApiGatewayRouteConfigService(_mockEnvironmentManager.Object, _mockLogger.Object);

        // Act
        var result = service.GetRouteConfig("GET", "/nonexistent/123");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Constructor_LoadsAndParsesListOfConfigs()
    {
        // Arrange
        var routeConfigs = new List<ApiGatewayRouteConfig>
        {
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "Function1",
                HttpMethod = "GET",
                Path = "/path1"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "Function2",
                HttpMethod = "POST",
                Path = "/path2"
            }
        };

        _mockEnvironmentManager
            .Setup(m => m.GetEnvironmentVariables())
            .Returns(new Dictionary<string, string>
            {
                { Constants.LambdaConfigEnvironmentVariablePrefix, JsonSerializer.Serialize(routeConfigs) }
            });

        var service = new ApiGatewayRouteConfigService(_mockEnvironmentManager.Object, _mockLogger.Object);

        // Act
        var result1 = service.GetRouteConfig("GET", "/path1");
        var result2 = service.GetRouteConfig("POST", "/path2");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("Function1", result1.LambdaResourceName);
        Assert.Equal("Function2", result2.LambdaResourceName);
    }
}