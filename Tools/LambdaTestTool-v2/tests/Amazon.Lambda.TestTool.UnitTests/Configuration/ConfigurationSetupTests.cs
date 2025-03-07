// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Amazon.Lambda.TestTool.Configuration;
using Moq;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.Configuration;

public class ConfigurationSetupTests
{
    [Fact]
    public void GetConfiguration_WithValidAssemblyLocation_ReturnsConfiguration()
    {
        // Arrange
        var mockAssembly = new Mock<Assembly>();
        var testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDirectory);

        try
        {
            // Create test appsettings.json
            File.WriteAllText(
                Path.Combine(testDirectory, "appsettings.json"),
                @"{""TestSetting"": ""TestValue""}"
            );

            mockAssembly
                .Setup(x => x.Location)
                .Returns(Path.Combine(testDirectory, "dummy.dll"));

            var configSetup = new ConfigurationSetup(mockAssembly.Object);

            // Act
            var configuration = configSetup.GetConfiguration();

            // Assert
            Assert.NotNull(configuration);
            Assert.Equal("TestValue", configuration["TestSetting"]);
        }
        finally
        {
            // Cleanup
            Directory.Delete(testDirectory, true);
        }
    }

    [Fact]
    public void GetConfiguration_WithEnvironmentSpecificConfig_LoadsBothConfigs()
    {
        // Arrange
        var mockAssembly = new Mock<Assembly>();
        var testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDirectory);

        try
        {
            // Create test appsettings.json
            File.WriteAllText(
                Path.Combine(testDirectory, "appsettings.json"),
                @"{""TestSetting"": ""BaseValue"", ""CommonSetting"": ""BaseValue""}"
            );

            // Create test appsettings.Development.json
            File.WriteAllText(
                Path.Combine(testDirectory, "appsettings.Development.json"),
                @"{""TestSetting"": ""DevValue""}"
            );

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            mockAssembly
                .Setup(x => x.Location)
                .Returns(Path.Combine(testDirectory, "dummy.dll"));

            var configSetup = new ConfigurationSetup(mockAssembly.Object);

            // Act
            var configuration = configSetup.GetConfiguration();

            // Assert
            Assert.NotNull(configuration);
            Assert.Equal("DevValue", configuration["TestSetting"]); // Overridden value
            Assert.Equal("BaseValue", configuration["CommonSetting"]); // Base value
        }
        finally
        {
            // Cleanup
            Directory.Delete(testDirectory, true);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void GetConfiguration_WithInvalidAssemblyLocation_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockAssembly = new Mock<Assembly>();
        mockAssembly
            .Setup(x => x.Location)
            .Returns(string.Empty);

        var configSetup = new ConfigurationSetup(mockAssembly.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => configSetup.GetConfiguration()
        );
        Assert.Equal("Unable to determine assembly location", exception.Message);
    }
}

