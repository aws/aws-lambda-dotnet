// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;
using Microsoft.AspNetCore.Components;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Models;
using BlazorMonaco.Editor;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Amazon.Lambda.TestTool.Components.Pages;

/// <summary>
/// Component representing the Lambda Test Tool.
/// </summary>
public partial class Home : ComponentBase, IDisposable
{
    /// <summary>
    /// <see cref="NavigationManager"/>
    /// </summary>
    [Inject] public required NavigationManager NavManager { get; set; }
    /// <summary>
    /// <see cref="ILogger{Home}"/>
    /// </summary>
    [Inject] public required ILogger<Home> Logger { get; set; }
    /// <summary>
    /// <see cref="IHttpContextAccessor"/>
    /// </summary>
    [Inject] public required IHttpContextAccessor HttpContextAccessor { get; set; }
    /// <summary>
    /// <see cref="IRuntimeApiDataStoreManager"/>
    /// </summary>
    [Inject] public required IRuntimeApiDataStoreManager DataStoreManager { get; set; }
    /// <summary>
    /// <see cref="IThemeService"/>
    /// </summary>
    [Inject] public required IThemeService ThemeService { get; set; }
    /// <summary>
    /// <see cref="IJSRuntime"/>
    /// </summary>
    [Inject] public required IJSRuntime JsRuntime { get; set; }
    /// <summary>
    /// <see cref="ILambdaClient"/>
    /// </summary>
    [Inject] public required ILambdaClient LambdaClient { get; set; }
    /// <summary>
    /// <see cref="IOptions{LambdaOptions}"/>
    /// </summary>
    [Inject] public required IOptions<LambdaOptions> LambdaOptions { get; set; }
    /// <summary>
    /// <see cref="IGlobalSettingsService"/>
    /// </summary>
    [Inject] public required IGlobalSettingsService GlobalSettingsService { get; set; }
    /// <summary>
    /// <see cref="LambdaRequestManager"/>
    /// </summary>
    [Inject] public required ILambdaRequestManager LambdaRequestManager { get; set; }

    private StandaloneCodeEditor? _editor;
    private StandaloneCodeEditor? _activeEditor;
    private StandaloneCodeEditor? _activeEditorError;

    private EventDialog? _eventDialog;
    private SaveRequestDialog? _saveRequestDialog;
    private ManageSavedRequestsDialog? _manageSavedRequestsDialog;

    private int _queuedEventsCount;
    private int _pastEventsCount;

    /// <summary>
    /// Reload Lambda input requests.
    /// </summary>
    public void ReloadSampleRequests()
    {
        if (SelectedFunctionName is not null)
            SampleRequests = LambdaRequestManager.GetLambdaRequests(SelectedFunctionName, includeSampleRequests: GlobalSettingsService.CurrentSettings.ShowSampleRequests, includeSavedRequests: !string.IsNullOrEmpty(LambdaOptions.Value.SavedRequestsPath) && GlobalSettingsService.CurrentSettings.ShowSavedRequests);

        StateHasChanged();
    }

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

            ReloadSampleRequests();

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
            _selectedSampleRequestName = value;
            if (string.IsNullOrEmpty(_selectedSampleRequestName) ||
                string.Equals(value, NoSampleSelectedId))
            {
                _editor?.SetValue(string.Empty);
            }
            else
            {
                if (SelectedFunctionName is not null)
                    _editor?.SetValue(LambdaRequestManager.GetRequest(SelectedFunctionName, _selectedSampleRequestName));
            }

            StateHasChanged();
        }
    }

    /// <summary>
    /// Method called at class initialization.
    /// </summary>
    protected override async Task OnInitializedAsync()
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
        _queuedEventsCount = DataStore?.QueuedEvents.Count ?? 0;
        _pastEventsCount = DataStore?.ExecutedEvents.Count ?? 0;

        await GlobalSettingsService.LoadSettingsAsync();
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

    /// <summary>
    /// Method called as instance disposal.
    /// </summary>
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

    private bool functionUpdated;

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

    async Task ShowSaveEventDialog()
    {
        if (_editor is null ||
            DataStore is null ||
            SelectedFunctionName is null)
            return;

        var editorValue = await _editor.GetValue();
        _saveRequestDialog?.ShowDialog(SelectedFunctionName, editorValue);
    }

    async Task ShowManagedSavedRequestsDialog()
    {
        if (_manageSavedRequestsDialog is null)
            return;

        await _manageSavedRequestsDialog.ShowDialog(SelectedFunctionName);
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
            await LambdaClient.InvokeAsync(invokeRequest, LambdaOptions.Value.Endpoint);
            _errorMessage = string.Empty;
            return true;
        }
        catch (AmazonLambdaException e)
        {
            Logger.LogInformation(e.Message);

            // lambda client automatically adds some extra verbiage: "The service returned an error with Error Code xxxx and HTTP Body: <bodyhere>".
            // removing the extra verbiage to make the error message smaller and look better on the ui.
            _errorMessage = e.Message.Contains("HTTP Body: ")
                ? e.Message.Split("HTTP Body: ")[1]
                : e.Message;
        }
        return false;
    }

    private string _editorContent = string.Empty;
    private async Task HandleModelContentChanged()
    {
        if (_editor is null)
            return;

        _editorContent = await _editor.GetValue();

        StateHasChanged();
    }
}
