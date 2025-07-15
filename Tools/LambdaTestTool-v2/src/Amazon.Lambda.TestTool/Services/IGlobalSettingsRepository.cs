// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// An interface for a repository responsible for retrieving and saving global settings.
/// </summary>
public interface IGlobalSettingsRepository
{
    /// <summary>
    /// Retrieve global settings from source.
    /// </summary>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns><see cref="GlobalSettings"/></returns>
    Task<GlobalSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save global settings to source.
    /// </summary>
    /// <param name="settings"><see cref="GlobalSettings"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    Task SaveSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken = default);
}
