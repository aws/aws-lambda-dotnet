// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Commands.Settings;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.Commands.Settings;

public class RunCommandSettingsTests
{
    [Fact]
    public void DefaultHost_IsSetToConstantsDefaultHost()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.Equal(Constants.DefaultLambdaEmulatorHost, settings.LambdaEmulatorHost);
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
    public void ApiGatewayEmulatorMode_DefaultsToNull()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.Null(settings.ApiGatewayEmulatorMode);
    }

    [Fact]
    public void SavedRequestsPath_DefaultsToNull()
    {
        // Arrange
        var settings = new RunCommandSettings();

        // Assert
        Assert.Null(settings.ConfigStoragePath);
    }
}
