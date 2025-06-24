// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// A service for accessing <see cref="GlobalSettings"/>.
/// </summary>
/// <param name="settingsRepository"><see cref="IGlobalSettingsRepository"/></param>
/// <param name="logger"><see cref="ILogger{GlobalSettingsService}"/></param>
public class GlobalSettingsService(
    IGlobalSettingsRepository settingsRepository,
    ILogger<GlobalSettingsService> logger) : IGlobalSettingsService, IAsyncDisposable
{
    private GlobalSettings _currentSettings = new ();
    private readonly SemaphoreSlim _settingsLock = new (1, 1); // For thread-safe updates

    /// <inheritdoc/>
    public async Task LoadSettingsAsync()
    {
        await _settingsLock.WaitAsync();
        try
        {
            _currentSettings = await settingsRepository.LoadSettingsAsync();
            logger.LogInformation("Global settings loaded successfully.");
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    /// <inheritdoc/>
    public GlobalSettings CurrentSettings
    {
        get
        {
            // Return a clone to prevent external modification of the internal state
            return _currentSettings.DeepCopy();
        }
    }

    /// <inheritdoc/>
    public async Task UpdateSettings(Action<GlobalSettings> updateAction)
    {
        // Use a lock to ensure thread-safe updates
        _settingsLock.Wait();
        try
        {
            var newSettings = _currentSettings.DeepCopy(); // Create a mutable copy to update
            updateAction(newSettings); // Apply changes

            if (!AreSettingsEqual(_currentSettings, newSettings))
            {
                _currentSettings = newSettings;
                logger.LogInformation("Global settings updated.");
                _ = settingsRepository.SaveSettingsAsync(_currentSettings); // Fire-and-forget save
            }

            await Task.CompletedTask;
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    // Comparison to avoid unnecessary saves if nothing changed
    private bool AreSettingsEqual(GlobalSettings s1, GlobalSettings s2)
    {
        return s1.ShowSampleRequests == s2.ShowSampleRequests &&
               s1.ShowSavedRequests == s2.ShowSavedRequests &&
               s1.ShowRequestsList == s2.ShowRequestsList;
    }

    /// <summary>
    /// Dispose of <see cref="GlobalSettingsService"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _settingsLock.Dispose();
        await Task.CompletedTask;
    }
}
