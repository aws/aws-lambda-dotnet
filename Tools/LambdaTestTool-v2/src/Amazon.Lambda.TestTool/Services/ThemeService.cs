// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Services;

/// <inheritdoc cref="IThemeService"/>
public class ThemeService : IThemeService
{
    private string? _currentTheme;

    /// <inheritdoc />
    public event Action? OnThemeChanged;

    /// <inheritdoc />
    public string CurrentTheme
    {
        get => _currentTheme ?? string.Empty;
        set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                NotifyThemeChanged();
            }
        }
    }

    /// <summary>
    /// Notifies subscribers that the theme has changed.
    /// </summary>
    private void NotifyThemeChanged()
    {
        OnThemeChanged?.Invoke();
    }
}
