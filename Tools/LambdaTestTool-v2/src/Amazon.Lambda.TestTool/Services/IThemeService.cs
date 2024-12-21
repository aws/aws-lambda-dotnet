// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// The theme service keeps track of the current theme and notifies subscribers on theme changes.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// An event to keep track of theme changes.
    /// </summary>
    event Action? OnThemeChanged;

    /// <summary>
    /// The currently applied theme.
    /// </summary>
    string CurrentTheme { get; set; }
}
