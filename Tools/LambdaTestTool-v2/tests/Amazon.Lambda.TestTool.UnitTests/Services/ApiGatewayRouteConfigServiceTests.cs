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

    [Fact]
    public void CatchAllRouteConfig()
    {
        // Arange
        var routeConfigs = new List<ApiGatewayRouteConfig>
        {
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F1",
                HttpMethod = "ANY",
                Path = "/{proxy+}"
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
        var result1 = service.GetRouteConfig("GET", "/a");
        var result2 = service.GetRouteConfig("POST", "/b");
        var result3 = service.GetRouteConfig("DELETE", "/c");
        var result4 = service.GetRouteConfig("GET", "/a/1");
        var result5 = service.GetRouteConfig("POST", "/b/2");
        var result6 = service.GetRouteConfig("DELETE", "/c/1/d");
        var result7 = service.GetRouteConfig("GET", "/a/a/a/a/a/a/a/a/a/a");
        var result8 = service.GetRouteConfig("GET", "/");
        
        // Assert
        Assert.Equal("F1", result1?.LambdaResourceName);
        Assert.Equal("F1", result2?.LambdaResourceName);
        Assert.Equal("F1", result3?.LambdaResourceName);
        Assert.Equal("F1", result4?.LambdaResourceName);
        Assert.Equal("F1", result5?.LambdaResourceName);
        Assert.Equal("F1", result6?.LambdaResourceName);
        Assert.Equal("F1", result7?.LambdaResourceName);
        Assert.Equal("F1", result8?.LambdaResourceName);
    }

    [Fact]
    public void RouteConfigTemplateLongerThanRequest()
    {
        // Arange
        var routeConfigs = new List<ApiGatewayRouteConfig>
        {
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F1",
                HttpMethod = "ANY",
                Path = "/resource/{id}/{proxy+}"
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
        var result1 = service.GetRouteConfig("GET", "/resource/123");
        
        // Assert
        Assert.Null(result1);
    }

    [Fact]
    public void RouteConfigRequestLongerThanTemplate()
    {
        // Arange
        var routeConfigs = new List<ApiGatewayRouteConfig>
        {
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F1",
                HttpMethod = "ANY",
                Path = "/resource/{id}"
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
        var result1 = service.GetRouteConfig("GET", "/resource/123/foo");
        
        // Assert
        Assert.Null(result1);
    }

    [Fact]
    public void ProperlyMatchRouteConfigs()
    {
        // Arrange
        var routeConfigs = new List<ApiGatewayRouteConfig>
        {
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F1",
                HttpMethod = "ANY",
                Path = "/{proxy+}"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F2",
                HttpMethod = "GET",
                Path = "/pe/{proxy+}"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F3",
                HttpMethod = "GET",
                Path = "/pets/{proxy+}"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F4",
                HttpMethod = "GET",
                Path = "/pets/dog/{id}/{id2}/{id3}"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F5",
                HttpMethod = "GET",
                Path = "/pets/{dog}/{id}"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F6",
                HttpMethod = "GET",
                Path = "/pets/dog/{id}"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F7",
                HttpMethod = "GET",
                Path = "/pets/dog/1"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F8",
                HttpMethod = "GET",
                Path = "/pets/dog/cat/1"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F9",
                HttpMethod = "GET",
                Path = "/resource/{id}/subsegment/{proxy+}"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F10",
                HttpMethod = "GET",
                Path = "/resource/{id}/subsegment/{id2}/{proxy+}"
            },
            new ApiGatewayRouteConfig
            {
                LambdaResourceName = "F11",
                HttpMethod = "GET",
                Path = "/resource/1/subsegment/3/{proxy+}"
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
        var result1 = service.GetRouteConfig("GET", "/pets/dog/cat/1");
        var result2 = service.GetRouteConfig("GET", "/pets/dog/1");
        var result3 = service.GetRouteConfig("GET", "/pets/dog/cat/2");
        var result4 = service.GetRouteConfig("GET", "/pets/dog/2");
        var result5 = service.GetRouteConfig("GET", "/pets/cat/dog");
        var result6 = service.GetRouteConfig("GET", "/pets/cat/dog/1");
        var result7 = service.GetRouteConfig("GET", "/pets/dog/1/2/3");
        var result8 = service.GetRouteConfig("GET", "/pets/dog/1/2/3/4");
        var result9 = service.GetRouteConfig("GET", "/pe/dog/cat/2");
        var result10 = service.GetRouteConfig("GET", "/pe/cat/dog/1");
        var result11 = service.GetRouteConfig("GET", "/pe/dog/1/2/3/4");
        var result12 = service.GetRouteConfig("GET", "/pet/dog/cat/2");
        var result13 = service.GetRouteConfig("GET", "/pet/cat/dog/1");
        var result14 = service.GetRouteConfig("GET", "/pet/dog/1/2/3/4");
        var result15 = service.GetRouteConfig("GET", "/resource/1/subsegment/more");
        var result16 = service.GetRouteConfig("GET", "/resource/1/subsegment/2/more");
        var result17 = service.GetRouteConfig("GET", "/resource/1/subsegment/3/more");
        var result18 = service.GetRouteConfig("GET", "/pe/");
        var result19 = service.GetRouteConfig("GET", "/pe");
        var result20 = service.GetRouteConfig("GET", "/");
        var result21 = service.GetRouteConfig("GET", "");
        
        // Assert
        Assert.Equal("F8", result1?.LambdaResourceName);
        Assert.Equal("F7", result2?.LambdaResourceName);
        Assert.Equal("F3", result3?.LambdaResourceName);
        Assert.Equal("F6", result4?.LambdaResourceName);
        Assert.Equal("F5", result5?.LambdaResourceName);
        Assert.Equal("F3", result6?.LambdaResourceName);
        Assert.Equal("F4", result7?.LambdaResourceName);
        Assert.Equal("F3", result8?.LambdaResourceName);
        Assert.Equal("F2", result9?.LambdaResourceName);
        Assert.Equal("F2", result10?.LambdaResourceName);
        Assert.Equal("F2", result11?.LambdaResourceName);
        Assert.Equal("F1", result12?.LambdaResourceName);
        Assert.Equal("F1", result13?.LambdaResourceName);
        Assert.Equal("F1", result14?.LambdaResourceName);
        Assert.Equal("F9", result15?.LambdaResourceName);
        Assert.Equal("F10", result16?.LambdaResourceName);
        Assert.Equal("F11", result17?.LambdaResourceName);
        Assert.Equal("F2", result18?.LambdaResourceName);
        Assert.Equal("F1", result19?.LambdaResourceName);
        Assert.Equal("F1", result20?.LambdaResourceName);
        Assert.Equal("F1", result21?.LambdaResourceName);
    }
}