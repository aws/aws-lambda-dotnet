// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;
using Amazon.Lambda.DurableExecution;
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
    /// <summary>
    /// Service provider used to optionally resolve the durable-execution driver, which is only
    /// registered when the tool was started with <c>--durable-execution</c>.
    /// </summary>
    [Inject] public required IServiceProvider ServiceProvider { get; set; }

    // Non-null only when --durable-execution is enabled; gates the durable invoke UI.
    private Services.DurableExecution.DurableExecutionDriver? _durableDriver;

    /// <summary>True when the durable-execution emulator is enabled and the durable invoke UI should show.</summary>
    private bool DurableExecutionEnabled => _durableDriver is not null;

    // Durable invoke form state.
    private bool _durableExecution;
    private string _durableExecutionName = string.Empty;

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
            SampleRequests = LambdaRequestManager.GetLambdaRequests(SelectedFunctionName, includeSampleRequests: GlobalSettingsService.CurrentSettings.ShowSampleRequests, includeSavedRequests: !string.IsNullOrEmpty(LambdaOptions.Value.ConfigStoragePath) && GlobalSettingsService.CurrentSettings.ShowSavedRequests);

        StateHasChanged();
    }

    /// <summary>
    /// Callback function on save request.
    /// </summary>
    public async Task OnSaveRequest()
    {
        await ShowToast("Request has been saved.");

        ReloadSampleRequests();
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
        // Resolve the durable driver optionally (only present with --durable-execution). Used to
        // gate the durable invoke UI and to re-run durable executions from Re-Invoke.
        _durableDriver = ServiceProvider.GetService(typeof(Services.DurableExecution.DurableExecutionDriver))
            as Services.DurableExecution.DurableExecutionDriver;

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

    void OnDurableExecutionToggled(ChangeEventArgs e)
    {
        _durableExecution = e.Value is true;
        StateHasChanged();
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

        // For a durable function the queued/executed events are internal replay envelopes
        // (DurableExecutionArn + InitialExecutionState), not user payloads. Re-posting one as a
        // plain invoke would just run a single replay pass against stale state — not a new
        // execution. Instead, extract the original user payload and start a FRESH durable
        // execution so "re-invoke" means "run the whole workflow again".
        if (DurableExecutionEnabled && TryGetDurableUserPayload(evnt.EventJson, out var userPayload))
        {
            var invokeRequest = new InvokeRequest
            {
                FunctionName = SelectedFunctionName,
                Payload = userPayload,
                InvocationType = InvocationType.Event,
                DurableExecutionName = Guid.NewGuid().ToString()
            };
            try
            {
                await LambdaClient.InvokeAsync(invokeRequest, LambdaOptions.Value.Endpoint);
                _errorMessage = string.Empty;
            }
            catch (AmazonLambdaException ex)
            {
                Logger.LogInformation(ex.Message);
                _errorMessage = ex.Message;
            }
            StateHasChanged();
            return;
        }

        await InvokeLambdaFunction(evnt.EventJson);
        StateHasChanged();
    }

    /// <summary>
    /// If <paramref name="eventJson"/> is a durable-execution replay envelope, extracts the
    /// original user input payload from its EXECUTION operation. Returns false for ordinary
    /// (non-durable) event JSON.
    /// </summary>
    private static bool TryGetDurableUserPayload(string eventJson, out string userPayload)
    {
        userPayload = string.Empty;
        if (string.IsNullOrWhiteSpace(eventJson))
            return false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(eventJson);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object
                || !root.TryGetProperty("DurableExecutionArn", out _))
            {
                return false; // not a durable envelope
            }

            // The user payload rides on the EXECUTION-type op's ExecutionDetails.InputPayload.
            if (root.TryGetProperty("InitialExecutionState", out var state)
                && state.ValueKind == System.Text.Json.JsonValueKind.Object
                && state.TryGetProperty("Operations", out var ops)
                && ops.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var op in ops.EnumerateArray())
                {
                    if (op.TryGetProperty("Type", out var type)
                        && type.GetString() == OperationTypes.Execution
                        && op.TryGetProperty("ExecutionDetails", out var details)
                        && details.ValueKind == System.Text.Json.JsonValueKind.Object
                        && details.TryGetProperty("InputPayload", out var input)
                        && input.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        userPayload = input.GetString() ?? string.Empty;
                        return !string.IsNullOrEmpty(userPayload);
                    }
                }
            }

            // It is a durable envelope but we couldn't find the payload (e.g. first invocation
            // before the EXECUTION op is echoed back). Fall back to an empty payload rather than
            // re-posting the raw envelope.
            userPayload = "{}";
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
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

        // When the durable checkbox is set, carry the durable-execution-name header so the tool's
        // start hook launches a durable execution (rather than a one-shot invoke). An empty name
        // lets the emulator auto-generate one.
        if (_durableExecution && DurableExecutionEnabled)
        {
            invokeRequest.DurableExecutionName = string.IsNullOrWhiteSpace(_durableExecutionName)
                ? Guid.NewGuid().ToString()
                : _durableExecutionName.Trim();
        }

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

    private string? _toastMessage;
    private async Task ShowToast(string message)
    {
        _toastMessage = message;
        await JsRuntime.InvokeVoidAsync("liveToast.show");
    }
}
