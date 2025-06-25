// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Amazon.Lambda.TestTool.Components.Pages;

/// <summary>
/// Dialog to save a Lambda input request.
/// </summary>
public partial class SaveRequestDialog : ComponentBase
{
    /// <summary>
    /// A callback function to be invoked on save.
    /// </summary>
    [Parameter]
    public EventCallback OnSaveRequest { get; set; }

    /// <summary>
    /// Service responsible for managing Lambda requests.
    /// </summary>
    [Inject] public required ILambdaRequestManager LambdaRequestManager { get; set; }

    /// <summary>
    /// Service responsible for invoking Javascript functions.
    /// </summary>
    [Inject] public required IJSRuntime JsRuntime { get; set; }

    private string? _functionName;
    private string? _requestBody;
    private string? _requestName;
    private string? _errorMessage;

    private readonly string modalId = "saveRequestModal";

    /// <summary>
    /// Used to set required properties for the save request dialog.
    /// </summary>
    /// <param name="functionName">Lambda function name</param>
    /// <param name="requestBody">The body of the request to be saved.</param>
    public void ShowDialog(string functionName, string? requestBody)
    {
        _functionName = functionName;
        _requestBody = requestBody;
        _requestName = null;
        _errorMessage = null;

        StateHasChanged();
    }

    /// <summary>
    /// Used to save requests to a specified location.
    /// </summary>
    public async Task OnSaveClick()
    {
        if (string.IsNullOrEmpty(_functionName))
            throw new InvalidSaveRequestException($"The Lambda function name is null or empty.");

        if (_requestBody is null)
            throw new InvalidSaveRequestException($"The request body is null or empty or ");

        if (string.IsNullOrEmpty(_requestName))
        {
            _errorMessage = "The request name is invalid.";
            return;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (_requestName.Any(c => invalidChars.Contains(c)))
        {
            _errorMessage = "The request name is invalid.";
            return;
        }

        LambdaRequestManager.SaveRequest(_functionName, _requestName, _requestBody);
        if (OnSaveRequest.HasDelegate)
        {
            await OnSaveRequest.InvokeAsync();
        }
        await JsRuntime.InvokeVoidAsync("bootstrapModal.hide", modalId);
    }
}
