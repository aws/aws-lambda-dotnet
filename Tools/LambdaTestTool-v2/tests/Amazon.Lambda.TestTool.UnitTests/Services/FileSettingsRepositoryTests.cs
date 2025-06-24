// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests.Services;

public class FileSettingsRepositoryTests
{
    private readonly Mock<IOptions<LambdaOptions>> _mockLambdaOptions;
    private readonly Mock<ILogger<FileSettingsRepository>> _mockLogger;
    private readonly string _tempPath;

    public FileSettingsRepositoryTests()
    {
        _mockLambdaOptions = new Mock<IOptions<LambdaOptions>>();
        _mockLogger = new Mock<ILogger<FileSettingsRepository>>();
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
    }

    private FileSettingsRepository CreateRepository(string? savedRequestsPath)
    {
        var options = new LambdaOptions { SavedRequestsPath = savedRequestsPath };
        _mockLambdaOptions.Setup(o => o.Value).Returns(options);
        return new FileSettingsRepository(_mockLambdaOptions.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task LoadSettingsAsync_NoPath_ReturnsDefaultSettings()
    {
        // Arrange
        var repository = CreateRepository(null);

        // Act
        var settings = await repository.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.ShowSampleRequests);
        Assert.True(settings.ShowSavedRequests);
        Assert.True(settings.ShowRequestsList);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("A saved requests path was not provided")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadSettingsAsync_FileNotExists_ReturnsDefaultSettings()
    {
        // Arrange
        var repository = CreateRepository(_tempPath);

        // Act
        var settings = await repository.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.ShowSampleRequests);
        Assert.True(settings.ShowSavedRequests);
        Assert.True(settings.ShowRequestsList);
        var expectedFilePath = Path.Combine(_tempPath, Constants.TestToolLocalDirectory, Constants.GlobalSettingsFileName);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Settings file not found at {expectedFilePath}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadSettingsAsync_FileExists_ReturnsSettings()
    {
        // Arrange
        var testToolDir = Path.Combine(_tempPath, Constants.TestToolLocalDirectory);
        Directory.CreateDirectory(testToolDir);
        var filePath = Path.Combine(testToolDir, Constants.GlobalSettingsFileName);

        var expectedSettings = new GlobalSettings
        {
            ShowSampleRequests = false,
            ShowSavedRequests = false,
            ShowRequestsList = true
        };
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(expectedSettings));

        var repository = CreateRepository(_tempPath);

        // Act
        var settings = await repository.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(expectedSettings.ShowSampleRequests, settings.ShowSampleRequests);
        Assert.Equal(expectedSettings.ShowSavedRequests, settings.ShowSavedRequests);
        Assert.Equal(expectedSettings.ShowRequestsList, settings.ShowRequestsList);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Settings loaded from {filePath}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadSettingsAsync_CorruptFile_ReturnsDefaultSettings()
    {
        // Arrange
        var testToolDir = Path.Combine(_tempPath, Constants.TestToolLocalDirectory);
        Directory.CreateDirectory(testToolDir);
        var filePath = Path.Combine(testToolDir, Constants.GlobalSettingsFileName);

        await File.WriteAllTextAsync(filePath, "this is not valid json");

        var repository = CreateRepository(_tempPath);

        // Act
        var settings = await repository.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.ShowSampleRequests);
        Assert.True(settings.ShowSavedRequests);
        Assert.True(settings.ShowRequestsList);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error loading settings from {filePath}")),
                It.IsAny<JsonException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveSettingsAsync_NoPath_DoesNothing()
    {
        // Arrange
        var repository = CreateRepository(null);
        var settings = new GlobalSettings();

        // Act
        await repository.SaveSettingsAsync(settings);

        // Assert
        var testToolDir = Path.Combine(_tempPath, Constants.TestToolLocalDirectory);
        Assert.False(Directory.Exists(testToolDir));
    }

    [Fact]
    public async Task SaveSettingsAsync_ValidPath_SavesFile()
    {
        // Arrange
        var repository = CreateRepository(_tempPath);
        var settings = new GlobalSettings
        {
            ShowRequestsList = false
        };

        // Act
        await repository.SaveSettingsAsync(settings);

        // Assert
        var testToolDir = Path.Combine(_tempPath, Constants.TestToolLocalDirectory);
        var filePath = Path.Combine(testToolDir, Constants.GlobalSettingsFileName);

        Assert.True(File.Exists(filePath));
        var content = await File.ReadAllTextAsync(filePath);
        var savedSettings = JsonSerializer.Deserialize<GlobalSettings>(content);
        Assert.NotNull(savedSettings);
        Assert.Equal(settings.ShowRequestsList, savedSettings.ShowRequestsList);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Settings saved to {filePath}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveSettingsAsync_DirectoryCreationError_LogsError()
    {
        // Arrange
        // Create a file where a directory should be to cause a conflict
        var conflictingPath = Path.Combine(_tempPath, Constants.TestToolLocalDirectory);
        await File.WriteAllTextAsync(conflictingPath, "i am a file");

        var repository = CreateRepository(_tempPath);
        var settings = new GlobalSettings();

        // Act
        await repository.SaveSettingsAsync(settings);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error saving settings to")),
                It.IsAny<IOException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
