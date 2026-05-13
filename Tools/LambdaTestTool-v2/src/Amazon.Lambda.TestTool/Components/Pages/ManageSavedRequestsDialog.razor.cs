// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Amazon.Lambda.TestTool.Components.Pages;

/// <summary>
/// Dialog to manage saved Lambda input requests.
/// </summary>
public partial class ManageSavedRequestsDialog : ComponentBase
{
    /// <summary>
    /// A callback function to be invoked on save.
    /// </summary>
    [Parameter]
    public EventCallback Callback { get; set; }

    /// <summary>
    /// A list of Lambda requests.
    /// </summary>
    public IDictionary<string, IList<LambdaRequest>>? SavedRequests { get; set; }

    /// <summary>
    /// A service to retrieve global settings.
    /// </summary>
    [Inject] public required IGlobalSettingsService GlobalSettingsService { get; set; }

    /// <summary>
    /// Service responsible for managing Lambda requests.
    /// </summary>
    [Inject] public required ILambdaRequestManager LambdaRequestManager { get; set; }

    /// <summary>
    /// Container for Lambda options.
    /// </summary>
    [Inject] public required IOptions<LambdaOptions> LambdaOptions { get; set; }

    private string? _functionName;

    /// <summary>
    /// Used to set required properties for the dialog.
    /// </summary>
    /// <param name="functionName">Lambda function name</param>
    public async Task ShowDialog(string? functionName)
    {
        _functionName = functionName;
        _showSampleRequests = GlobalSettingsService.CurrentSettings.ShowSampleRequests;
        _showSavedRequests = GlobalSettingsService.CurrentSettings.ShowSavedRequests;
        _showRequestsList = GlobalSettingsService.CurrentSettings.ShowRequestsList;

        await ReloadSampleRequests();

        if (!string.IsNullOrEmpty(functionName))
            SavedRequests = LambdaRequestManager.GetLambdaRequests(functionName, includeSavedRequests: !string.IsNullOrEmpty(LambdaOptions.Value.ConfigStoragePath), includeSampleRequests: false);

        StateHasChanged();
    }

    private bool _showSampleRequests = true;
    private bool _showSavedRequests = true;
    private bool _showRequestsList = true;

    private async Task ReloadSampleRequests()
    {
        await GlobalSettingsService.UpdateSettingsAsync(s =>
        {
            s.ShowSampleRequests = _showSampleRequests;
            s.ShowSavedRequests = _showSavedRequests;
            s.ShowRequestsList = _showRequestsList;
        });
        if (Callback.HasDelegate)
        {
            await Callback.InvokeAsync();
        }
    }

    private async Task DeleteSavedRequest(string requestName)
    {
        if (string.IsNullOrEmpty(_functionName))
            return;

        LambdaRequestManager.DeleteRequest(_functionName, requestName);

        await ReloadSampleRequests();

        SavedRequests = LambdaRequestManager.GetLambdaRequests(_functionName, includeSavedRequests: !string.IsNullOrEmpty(LambdaOptions.Value.ConfigStoragePath), includeSampleRequests: false);

        StateHasChanged();
    }
}
