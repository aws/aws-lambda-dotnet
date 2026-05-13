// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;

namespace Amazon.Lambda.TestTool.Components.Pages;

public partial class EventDialog : ComponentBase
{
    [Inject] public required IThemeService ThemeService { get; set; }

    private EventContainer? _eventContainer;
    private StandaloneCodeEditor? _requestEditor;
    private StandaloneCodeEditor? _responseErrorEditor;
    private StandaloneCodeEditor? _responseEditor;

    public void ShowDialog(EventContainer eventContainer)
    {
        _eventContainer = eventContainer;
        _requestEditor?.SetValue(_eventContainer.EventJson);
        _responseErrorEditor?.SetValue(_eventContainer.ErrorResponse);
        _responseEditor?.SetValue(_eventContainer.Response);
        StateHasChanged();
    }

    private void ShowRequestTab()
    {
        _requestEditor?.SetValue(_eventContainer?.EventJson);
        StateHasChanged();
    }

    private void ShowResponseTab()
    {
        _responseErrorEditor?.SetValue(_eventContainer?.ErrorResponse);
        _responseEditor?.SetValue(_eventContainer?.Response);
        StateHasChanged();
    }

    private string GetStatusBadgeStyle(EventContainer.Status? status) => status switch
    {
        EventContainer.Status.Success => "text-bg-success",
        EventContainer.Status.Failure => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            Language = "json",
            GlyphMargin = false,
            Theme = ThemeService.CurrentTheme.Equals("dark") ? "vs-dark" : "vs",
            FontSize = 12,
            AutomaticLayout = true,
            ScrollBeyondLastLine = false,
            ReadOnly = true,
            Minimap = new EditorMinimapOptions
            {
                Enabled = false
            }
        };
    }
}
