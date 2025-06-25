// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.Services;

public class GlobalSettingsServiceTests
{
    private readonly Mock<IGlobalSettingsRepository> _mockSettingsRepository;
    private readonly Mock<ILogger<GlobalSettingsService>> _mockLogger;
    private readonly GlobalSettingsService _globalSettingsService;

    public GlobalSettingsServiceTests()
    {
        _mockSettingsRepository = new Mock<IGlobalSettingsRepository>();
        _mockLogger = new Mock<ILogger<GlobalSettingsService>>();
        _globalSettingsService = new GlobalSettingsService(_mockSettingsRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task LoadSettingsAsync_LoadsFromRepository()
    {
        // Arrange
        var expectedSettings = new GlobalSettings { ShowRequestsList = false };
        _mockSettingsRepository.Setup(r => r.LoadSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSettings);

        // Act
        await _globalSettingsService.LoadSettingsAsync();
        var actualSettings = _globalSettingsService.CurrentSettings;

        // Assert
        Assert.False(actualSettings.ShowRequestsList);
        _mockSettingsRepository.Verify(r => r.LoadSettingsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Global settings loaded successfully.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CurrentSettings_ReturnsDeepCopy()
    {
        // Arrange
        var initialSettings = _globalSettingsService.CurrentSettings;
        Assert.True(initialSettings.ShowSampleRequests);

        // Act
        initialSettings.ShowSampleRequests = false;

        var newSettings = _globalSettingsService.CurrentSettings;

        // Assert
        Assert.True(newSettings.ShowSampleRequests);
        Assert.NotSame(initialSettings, newSettings);
    }

    [Fact]
    public async Task UpdateSettings_WhenChanged_SavesAndUpdates()
    {
        // Arrange
        Assert.True(_globalSettingsService.CurrentSettings.ShowSampleRequests);

        // Act
        await _globalSettingsService.UpdateSettingsAsync(s => s.ShowSampleRequests = false);
        var updatedSettings = _globalSettingsService.CurrentSettings;

        // Assert
        Assert.False(updatedSettings.ShowSampleRequests);
        _mockSettingsRepository.Verify(r => r.SaveSettingsAsync(It.IsAny<GlobalSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Global settings updated.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSettings_WhenUnchanged_DoesNotSave()
    {
        // Arrange
        Assert.True(_globalSettingsService.CurrentSettings.ShowSampleRequests);

        // Act
        await _globalSettingsService.UpdateSettingsAsync(s => s.ShowSampleRequests = true);

        // Assert
        _mockSettingsRepository.Verify(r => r.SaveSettingsAsync(It.IsAny<GlobalSettings>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Global settings updated.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
