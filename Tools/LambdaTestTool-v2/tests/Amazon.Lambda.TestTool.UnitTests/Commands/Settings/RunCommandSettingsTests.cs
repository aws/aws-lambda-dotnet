// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Commands.Settings;

namespace Amazon.Lambda.TestTool.UnitTests.Commands.Settings;

public class RunCommandSettingsTests
{
    [Fact]
    public void DefaultHost_IsSetToConstantsDefaultHost()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.Equal(Constants.DefaultHost, settings.Host);
    }

    [Fact]
    public void DefaultPort_IsSetToConstantsDefaultPort()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.Equal(Constants.DefaultLambdaRuntimeEmulatorPort, settings.Port);
    }

    [Fact]
    public void ApiGatewayEmulatorPort_IsSetToConstantsDefaultApiGatewayEmulatorPort()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.Equal(Constants.DefaultApiGatewayEmulatorPort, settings.ApiGatewayEmulatorPort);
    }

    [Fact]
    public void NoLaunchWindow_DefaultsToFalse()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.False(settings.NoLaunchWindow);
    }

    [Fact]
    public void DisableLogs_DefaultsToFalse()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.False(settings.DisableLogs);
    }

    [Fact]
    public void PauseExit_DefaultsToFalse()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.False(settings.PauseExit);
    }

    [Fact]
    public void ApiGatewayEmulatorMode_DefaultsToNull()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.Null(settings.ApiGatewayEmulatorMode);
    }
}
