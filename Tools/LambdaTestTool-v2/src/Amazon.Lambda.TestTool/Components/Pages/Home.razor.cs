// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;
using Microsoft.AspNetCore.Components;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.SampleRequests;
using Amazon.Lambda.TestTool.Services.IO;
using BlazorMonaco.Editor;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.WebUtilities;

namespace Amazon.Lambda.TestTool.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] public required NavigationManager NavManager { get; set; }
    [Inject] public required ILogger<Home> Logger { get; set; }
    [Inject] public required IHttpContextAccessor HttpContextAccessor { get; set; }
    [Inject] public required IRuntimeApiDataStoreManager DataStoreManager { get; set; }
    [Inject] public required IDirectoryManager DirectoryManager { get; set; }
    [Inject] public required IThemeService ThemeService { get; set; }
    [Inject] public required IJSRuntime JsRuntime { get; set; }

    [Inject] public required ILambdaClient LambdaClient { get; set; }

    private StandaloneCodeEditor? _editor;
    private StandaloneCodeEditor? _activeEditor;
    private StandaloneCodeEditor? _activeEditorError;

    private EventDialog? _eventDialog;

    private int _queuedEventsCount;
    private int _pastEventsCount;

    private const string NoSampleSelectedId = "void-select-request";

    private string _errorMessage = string.Empty;

    private IDictionary<string, IList<LambdaRequest>> SampleRequests { get; set; } = new Dictionary<string, IList<LambdaRequest>>();

    private IRuntimeApiDataStore? DataStore { get; set; }

    string? _selectedFunctionName;

    string? SelectedFunctionName
    {
        get => _selectedFunctionName;
        set
        {
            if (string.IsNullOrEmpty(value))
                return;
            _selectedFunctionName = value;
            if (DataStore != null)
                DataStore.StateChange -= DataStoreOnStateChange;
            DataStore = DataStoreManager.GetLambdaRuntimeDataStore(_selectedFunctionName);
            DataStore.StateChange += DataStoreOnStateChange;


            if (_activeEditor != null && !string.IsNullOrEmpty(DataStore?.ActiveEvent?.Response))
                _activeEditor.SetValue(DataStore.ActiveEvent.Response);
            if (_activeEditorError != null && !string.IsNullOrEmpty(DataStore?.ActiveEvent?.ErrorResponse))
                _activeEditorError.SetValue(DataStore.ActiveEvent?.ErrorResponse);

            _queuedEventsCount = DataStore?.QueuedEvents.Count ?? 0;
            _pastEventsCount = DataStore?.ExecutedEvents.Count ?? 0;

            StateHasChanged();
        }
    }
    List<string> _availableLambdaFunctions = new();
    string? _selectedSampleRequestName;
    string? SelectedSampleRequestName
    {
        get => _selectedSampleRequestName;
        set
        {
            if (SampleRequestManager == null)
                return;

            _selectedSampleRequestName = value;
            if (string.IsNullOrEmpty(_selectedSampleRequestName) ||
                string.Equals(value, NoSampleSelectedId))
            {
                _editor?.SetValue(string.Empty);
            }
            else
            {
                _editor?.SetValue(SampleRequestManager.GetRequest(_selectedSampleRequestName));
            }

            StateHasChanged();
        }
    }

    SampleRequestManager? SampleRequestManager { get; set; }

    protected override void OnInitialized()
    {
        var uri = NavManager.ToAbsoluteUri(NavManager.Uri);
        string initialFunction = string.Empty;
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("function", out var queryValue))
        {
            initialFunction = queryValue.ToString();
        }

        Logger.LogDebug("Query string variable for initial Lambda function set to: {initialFunction}", initialFunction);

        _availableLambdaFunctions = DataStoreManager.GetListOfFunctionNames().ToList();
        if (_availableLambdaFunctions.Count > 0)
        {
            if (!string.IsNullOrEmpty(initialFunction) && _availableLambdaFunctions.Contains(initialFunction))
            {
                Logger.LogDebug("Query string function found in the list of available functions");
                SelectedFunctionName = initialFunction;
            }
            else
            {
                Logger.LogDebug("Query string function not found in the list of available functions");
                SelectedFunctionName = _availableLambdaFunctions.First();
            }

            DataStore = DataStoreManager.GetLambdaRuntimeDataStore(SelectedFunctionName);
        }
        else
        {
            Logger.LogDebug("No functions currently registered with the test tool so default to the default function datastore");
            DataStore = DataStoreManager.GetLambdaRuntimeDataStore(LambdaRuntimeApi.DefaultFunctionName);
        }


        ThemeService.OnThemeChanged += HandleThemeChanged;
        DataStoreManager.StateChange += DataStoreManagerOnStateChange;
        SampleRequestManager = new SampleRequestManager(DirectoryManager.GetCurrentDirectory());
        SampleRequests = SampleRequestManager.GetSampleRequests();
        _queuedEventsCount = DataStore?.QueuedEvents.Count ?? 0;
        _pastEventsCount = DataStore?.ExecutedEvents.Count ?? 0;
    }

    private void HandleThemeChanged()
    {
        if (ThemeService.CurrentTheme.Equals("dark"))
        {
            Global.SetTheme(JsRuntime, "vs-dark");
        }
        else
        {
            Global.SetTheme(JsRuntime, "vs");
        }
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ThemeService.OnThemeChanged -= HandleThemeChanged;
    }

    private void DataStoreOnStateChange(object? sender, EventArgs e)
    {
        InvokeAsync(() =>
        {
            _queuedEventsCount = DataStore?.QueuedEvents.Count ?? 0;
            _pastEventsCount = DataStore?.ExecutedEvents.Count ?? 0;
            StateHasChanged();
            if (_activeEditor != null)
                _activeEditor.SetValue(DataStore?.ActiveEvent?.Response);
            if (_activeEditorError != null)
                _activeEditorError.SetValue(DataStore?.ActiveEvent?.ErrorResponse);
        });
    }

    private bool functionUpdated = false;

    private void DataStoreManagerOnStateChange(object? sender, EventArgs e)
    {
        InvokeAsync(() =>
        {
            _availableLambdaFunctions = DataStoreManager.GetListOfFunctionNames().ToList();
            SelectedFunctionName = _availableLambdaFunctions.FirstOrDefault();
            functionUpdated = true;
            StateHasChanged();
        });
    }

    async Task OnAddEventClick()
    {
        if (_editor is null ||
            DataStore is null)
            return;
        var editorValue = await _editor.GetValue();
        var success = await InvokeLambdaFunction(editorValue);
        if (success)
        {
            await _editor.SetValue(string.Empty);
            SelectedSampleRequestName = NoSampleSelectedId;
        }
        StateHasChanged();
    }

    void OnClearQueued()
    {
        DataStore?.ClearQueued();
        StateHasChanged();
    }

    void OnClearExecuted()
    {
        DataStore?.ClearExecuted();
        StateHasChanged();
    }

    async Task OnRequeue(string awsRequestId)
    {
        if (DataStore is null)
            return;
        EventContainer? evnt;
        if (string.Equals(DataStore.ActiveEvent?.AwsRequestId, awsRequestId))
        {
            evnt = DataStore.ActiveEvent;
        }
        else
        {
            evnt = DataStore.ExecutedEvents.FirstOrDefault(x => string.Equals(x.AwsRequestId, awsRequestId));
        }

        if (evnt == null)
            return;
        await InvokeLambdaFunction(evnt.EventJson);
        StateHasChanged();
    }

    void OnDeleteEvent(string awsRequestId)
    {
        DataStore?.DeleteEvent(awsRequestId);
        StateHasChanged();
    }

    string GetStatusBadgeStyle(EventContainer.Status status) => status switch
    {
        EventContainer.Status.Success => "text-bg-success",
        EventContainer.Status.Failure => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    string CreateSnippet(string? fullString)
    {
        const int maxLength = 50;
        string trim;
        if (fullString == null || fullString.Length < maxLength)
        {
            trim = fullString ?? string.Empty;
        }
        else
        {
            trim = fullString.Substring(0, maxLength);
        }

        return trim;
    }

    void ShowEvent(EventContainer evnt)
    {
        _eventDialog?.ShowDialog(evnt);
    }

    string GetLambdaFunctionName(string? functionId)
    {
        if (string.IsNullOrEmpty(functionId))
            return string.Empty;

        if (LambdaRuntimeApi.DefaultFunctionName.Equals(functionId))
            return "Default Lambda Function";

        return functionId;
    }

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            Language = "json",
            GlyphMargin = false,
            Theme = ThemeService.CurrentTheme.Equals("dark") ? "vs-dark" : "vs",
            AutomaticLayout = true,
            ScrollBeyondLastLine = false,
            Minimap = new EditorMinimapOptions
            {
                Enabled = false
            }
        };
    }

    private StandaloneEditorConstructionOptions ActiveEditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            Language = "json",
            GlyphMargin = false,
            Theme = ThemeService.CurrentTheme.Equals("dark") ? "vs-dark" : "vs",
            Value = DataStore?.ActiveEvent?.Response,
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

    void SetActiveLambdaFunction(string function)
    {
        SelectedFunctionName = function;
    }

    private StandaloneEditorConstructionOptions ActiveErrorEditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            Language = "json",
            GlyphMargin = false,
            Theme = ThemeService.CurrentTheme.Equals("dark") ? "vs-dark" : "vs",
            Value = DataStore?.ActiveEvent?.ErrorResponse,
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

    private async Task<bool> InvokeLambdaFunction(string payload)
    {
        var invokeRequest = new InvokeRequest
        {
            FunctionName = SelectedFunctionName,
            Payload = payload,
            InvocationType = InvocationType.Event
        };

        try
        {
            await LambdaClient.InvokeAsync(invokeRequest);
            return true;
        }
        catch (AmazonLambdaException e)
        {
            _errorMessage = e.Message;
        }
        return false;
    }
}
