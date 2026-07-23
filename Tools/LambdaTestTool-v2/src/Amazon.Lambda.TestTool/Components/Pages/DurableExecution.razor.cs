// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.TestTool.Services.DurableExecution;
using Microsoft.AspNetCore.Components;

namespace Amazon.Lambda.TestTool.Components.Pages;

/// <summary>
/// Web UI for the durable-execution emulator: lists local durable executions, shows each one's
/// operation timeline, and lets the developer resolve a parked callback ("Send Callback").
/// Only meaningful when the Test Tool was started with <c>--durable-execution</c>; otherwise the
/// driver is not registered and the page shows a hint.
/// </summary>
public partial class DurableExecution : ComponentBase, IDisposable
{
    // Resolve the driver optionally: it is only registered when --durable-execution was passed,
    // and [Inject] on a missing service would throw. Pulling it from the provider lets the page
    // render a friendly "not enabled" state instead.
    [Inject] public required IServiceProvider ServiceProvider { get; set; }

    internal DurableExecutionDriver? Driver { get; private set; }

    private IReadOnlyList<DurableExecutionDriver.ExecutionRecord> _executions = new List<DurableExecutionDriver.ExecutionRecord>();
    private string? _selectedArn;
    private IReadOnlyList<Operation> _operations = new List<Operation>();

    // Send-callback form state.
    private string _callbackId = string.Empty;
    private string _callbackResult = "\"approved\"";
    private string? _callbackFeedback;

    protected override void OnInitialized()
    {
        Driver = (DurableExecutionDriver?)ServiceProvider.GetService(typeof(DurableExecutionDriver));
        if (Driver is not null)
        {
            Driver.StateChange += OnDriverStateChange;
            Refresh();
        }
    }

    private void OnDriverStateChange(object? sender, EventArgs e) => InvokeAsync(() =>
    {
        Refresh();
        StateHasChanged();
    });

    private void Refresh()
    {
        if (Driver is null)
            return;

        _executions = Driver.GetExecutions()
            .OrderBy(r => r.Arn, StringComparer.Ordinal)
            .ToList();

        // Keep a selection; default to the first execution.
        _selectedArn ??= _executions.FirstOrDefault()?.Arn;
        if (_selectedArn is not null)
            _operations = Driver.GetOperations(_selectedArn);
    }

    private void SelectExecution(string arn)
    {
        _selectedArn = arn;
        _callbackFeedback = null;
        if (Driver is not null)
            _operations = Driver.GetOperations(arn);
        StateHasChanged();
    }

    private void PrefillCallback(string callbackId)
    {
        _callbackId = callbackId;
        _callbackFeedback = null;
        StateHasChanged();
    }

    private void SendCallbackSuccess()
    {
        if (Driver is null)
            return;

        if (string.IsNullOrWhiteSpace(_callbackId))
        {
            _callbackFeedback = "Enter a callback id first.";
            StateHasChanged();
            return;
        }

        var outcome = Driver.SendCallback(_callbackId, _callbackResult, error: null);
        _callbackFeedback = outcome switch
        {
            DurableExecutionStore.CallbackResolution.Resolved => $"Callback '{_callbackId}' resolved; execution resumed.",
            DurableExecutionStore.CallbackResolution.AlreadyResolved => $"Callback '{_callbackId}' was already completed.",
            _ => $"Unknown callback id '{_callbackId}'."
        };
        Refresh();
        StateHasChanged();
    }

    private static string PhaseBadgeClass(DurableExecutionDriver.ExecutionPhase phase) => phase switch
    {
        DurableExecutionDriver.ExecutionPhase.Succeeded => "text-bg-success",
        DurableExecutionDriver.ExecutionPhase.Failed => "text-bg-danger",
        DurableExecutionDriver.ExecutionPhase.ParkedOnCallback => "text-bg-warning",
        _ => "text-bg-secondary"
    };

    private static string StatusBadgeClass(string? status) => status switch
    {
        OperationStatuses.Succeeded => "text-bg-success",
        OperationStatuses.Failed => "text-bg-danger",
        OperationStatuses.Cancelled or OperationStatuses.Stopped or OperationStatuses.TimedOut => "text-bg-danger",
        OperationStatuses.Started or OperationStatuses.Pending => "text-bg-warning",
        OperationStatuses.Ready => "text-bg-info",
        _ => "text-bg-secondary"
    };

    /// <summary>The callback id awaiting resolution for an op, if this is a started CALLBACK.</summary>
    private static string? PendingCallbackId(Operation op) =>
        op.Type == OperationTypes.Callback && op.Status == OperationStatuses.Started
            ? op.CallbackDetails?.CallbackId
            : null;

    public void Dispose()
    {
        if (Driver is not null)
            Driver.StateChange -= OnDriverStateChange;
    }
}
