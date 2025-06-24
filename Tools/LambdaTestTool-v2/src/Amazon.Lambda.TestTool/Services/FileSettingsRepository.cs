// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Amazon.Lambda.TestTool.Models;
using Microsoft.Extensions.Options;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// Repository used to retrieve and save global settings in a local file.
/// </summary>
public class FileSettingsRepository : IGlobalSettingsRepository
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<FileSettingsRepository> _logger;
    private readonly IOptions<LambdaOptions> _lambdaOptions;

    /// <summary>
    /// Constructs an instance of <see cref="FileSettingsRepository"/>.
    /// </summary>
    /// <param name="lambdaOptions">Lambda options defined at startup</param>
    /// <param name="logger">Logger for <see cref="FileSettingsRepository"/></param>
    public FileSettingsRepository(IOptions<LambdaOptions> lambdaOptions, ILogger<FileSettingsRepository> logger)
    {
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        _logger = logger;
        _lambdaOptions = lambdaOptions;

        if (string.IsNullOrEmpty(lambdaOptions.Value.SavedRequestsPath))
        {
            logger.LogInformation("A saved requests path was not provided. Settings will not be persisted.");
        }
    }

    /// <inheritdoc/>
    public async Task<GlobalSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_lambdaOptions.Value.SavedRequestsPath)) return new ();
        var testToolDirectory = Path.Combine(_lambdaOptions.Value.SavedRequestsPath, Constants.TestToolLocalDirectory);
        var filePath = Path.Combine(testToolDirectory, Constants.GlobalSettingsFileName);
        if (!File.Exists(filePath))
        {
            _logger.LogInformation("Settings file not found at {FilePath}. Returning default settings.", filePath);
            return new GlobalSettings();
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var settings = await JsonSerializer.DeserializeAsync<GlobalSettings>(stream, _jsonOptions, cancellationToken);
            _logger.LogInformation("Settings loaded from {FilePath}.", filePath);
            return settings ?? new GlobalSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings from {FilePath}. Returning default settings.", filePath);
            return new GlobalSettings();
        }
    }

    /// <inheritdoc/>
    public async Task SaveSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_lambdaOptions.Value.SavedRequestsPath)) return;
        var testToolDirectory = Path.Combine(_lambdaOptions.Value.SavedRequestsPath, Constants.TestToolLocalDirectory);
        var filePath = Path.Combine(testToolDirectory, Constants.GlobalSettingsFileName);
        try
        {
            if (!Directory.Exists(testToolDirectory))
                Directory.CreateDirectory(testToolDirectory);

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
            _logger.LogInformation("Settings saved to {FilePath}.", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings to {FilePath}.", filePath);
        }
    }
}
