// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// A service for accessing <see cref="GlobalSettings"/>.
/// </summary>
public interface IGlobalSettingsService
{
    /// <summary>
    /// Current value of <see cref="GlobalSettings"/>.
    /// </summary>
    GlobalSettings CurrentSettings { get; }

    /// <summary>
    /// Update the <see cref="GlobalSettings"/>.
    /// </summary>
    /// <param name="updateAction"></param>
    /// <returns></returns>
    Task UpdateSettingsAsync(Action<GlobalSettings> updateAction);

    /// <summary>
    /// Initial load of <see cref="GlobalSettings"/>.
    /// </summary>
    /// <returns></returns>
    Task LoadSettingsAsync();
}
