// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using SdkOperation = Amazon.Lambda.DurableExecution.Operation;

namespace Amazon.Lambda.TestTool.Services.DurableExecution;

// Plain System.Text.Json models for the durable-execution data-plane request bodies.
//
// The AWSSDK.Lambda types (Amazon.Lambda.Model.OperationUpdate et al.) can't be used to
// deserialize the inbound wire JSON: their Action/Type/Status properties are AWSSDK
// ConstantClass values with no STJ contract. So the emulator owns these plain DTOs for the
// *inbound* direction. Property names mirror lambda-2015-03-31.normal.json exactly.
//
// Responses go the other way: we serialize the public Amazon.Lambda.DurableExecution.Operation
// type, whose JsonPropertyName attributes already match the wire, so no response DTOs are needed.

/// <summary>Wire body for <c>POST /2025-12-01/durable-executions/{arn}/checkpoint</c>.</summary>
internal sealed class CheckpointRequestBody
{
    [JsonPropertyName("CheckpointToken")]
    public string? CheckpointToken { get; set; }

    [JsonPropertyName("ClientToken")]
    public string? ClientToken { get; set; }

    [JsonPropertyName("Updates")]
    public List<WireOperationUpdate> Updates { get; set; } = new();

    // DurableExecutionArn rides in the URL path, not the body.
}

/// <summary>Wire body for <c>POST /2025-12-01/durable-execution-callbacks/{callbackId}/{succeed|fail}</c>.</summary>
internal sealed class CallbackRequestBody
{
    [JsonPropertyName("Payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("Error")]
    public WireErrorObject? Error { get; set; }
}

/// <summary>
/// Plain-STJ mirror of <c>Amazon.Lambda.Model.OperationUpdate</c>. Enum-like members
/// (<see cref="Type"/>, <see cref="Action"/>, <see cref="SubType"/>) are plain strings —
/// the emulator's checkpoint processor compares them against the
/// <c>Amazon.Lambda.DurableExecution.OperationTypes</c>/<c>OperationStatuses</c> constants.
/// </summary>
internal sealed class WireOperationUpdate
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("ParentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    [JsonPropertyName("SubType")]
    public string? SubType { get; set; }

    [JsonPropertyName("Action")]
    public string? Action { get; set; }

    [JsonPropertyName("Payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("Error")]
    public WireErrorObject? Error { get; set; }

    [JsonPropertyName("ContextOptions")]
    public WireContextOptions? ContextOptions { get; set; }

    [JsonPropertyName("StepOptions")]
    public WireStepOptions? StepOptions { get; set; }

    [JsonPropertyName("WaitOptions")]
    public WireWaitOptions? WaitOptions { get; set; }

    [JsonPropertyName("CallbackOptions")]
    public WireCallbackOptions? CallbackOptions { get; set; }

    [JsonPropertyName("ChainedInvokeOptions")]
    public WireChainedInvokeOptions? ChainedInvokeOptions { get; set; }
}

internal sealed class WireStepOptions
{
    [JsonPropertyName("NextAttemptDelaySeconds")]
    public int? NextAttemptDelaySeconds { get; set; }
}

internal sealed class WireWaitOptions
{
    [JsonPropertyName("WaitSeconds")]
    public long? WaitSeconds { get; set; }
}

internal sealed class WireCallbackOptions
{
    [JsonPropertyName("TimeoutSeconds")]
    public long? TimeoutSeconds { get; set; }

    [JsonPropertyName("HeartbeatTimeoutSeconds")]
    public long? HeartbeatTimeoutSeconds { get; set; }
}

internal sealed class WireChainedInvokeOptions
{
    [JsonPropertyName("FunctionName")]
    public string? FunctionName { get; set; }

    [JsonPropertyName("TenantId")]
    public string? TenantId { get; set; }
}

internal sealed class WireContextOptions
{
    [JsonPropertyName("ReplayChildren")]
    public bool? ReplayChildren { get; set; }
}

internal sealed class WireErrorObject
{
    [JsonPropertyName("ErrorType")]
    public string? ErrorType { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("ErrorData")]
    public string? ErrorData { get; set; }

    [JsonPropertyName("StackTrace")]
    public List<string>? StackTrace { get; set; }

    public static WireErrorObject? From(Amazon.Lambda.DurableExecution.ErrorObject? error)
    {
        if (error is null) return null;
        return new WireErrorObject
        {
            ErrorType = error.ErrorType,
            ErrorMessage = error.ErrorMessage,
            ErrorData = error.ErrorData,
            StackTrace = error.StackTrace?.ToList()
        };
    }
}

// ---- Response bodies -------------------------------------------------------------------------
// Serialized back to the SDK, which unmarshals them with the AWSSDK rest-json reader (NOT
// System.Text.Json). Two consequences drive these DTOs rather than reusing the public
// Amazon.Lambda.DurableExecution.Operation POCO:
//   1. rest-json `timestamp` members are unix SECONDS (a JSON number), whereas the POCO
//      serializes timestamps as epoch-MILLIS longs — the SDK would read 1.7e12 as seconds and
//      overflow DateTime. So timestamps here are doubles carrying seconds.
//   2. Everything else (enum-like strings, payloads, nested details) marshals cleanly by name.
// WireOperation.From maps the emulator's millis-based Operation into this wire shape.

/// <summary>Response body for <c>CheckpointDurableExecution</c>.</summary>
internal sealed class CheckpointResponseBody
{
    [JsonPropertyName("CheckpointToken")]
    public string? CheckpointToken { get; set; }

    [JsonPropertyName("NewExecutionState")]
    public NewExecutionStateBody? NewExecutionState { get; set; }
}

/// <summary>The <c>CheckpointUpdatedExecutionState</c> shape returned inside a checkpoint response.</summary>
internal sealed class NewExecutionStateBody
{
    [JsonPropertyName("Operations")]
    public List<WireOperation> Operations { get; set; } = new();

    [JsonPropertyName("NextMarker")]
    public string? NextMarker { get; set; }
}

/// <summary>Response body for <c>GetDurableExecutionState</c>.</summary>
internal sealed class StateResponseBody
{
    [JsonPropertyName("Operations")]
    public List<WireOperation> Operations { get; set; } = new();

    [JsonPropertyName("NextMarker")]
    public string? NextMarker { get; set; }
}

/// <summary>
/// Wire shape of an <c>Operation</c> for outbound (response) serialization. Mirrors
/// <c>Amazon.Lambda.Model.Operation</c> so the AWSSDK rest-json unmarshaller reads it faithfully;
/// timestamps are unix seconds (doubles), converted from the emulator's epoch-millis store.
/// </summary>
internal sealed class WireOperation
{
    [JsonPropertyName("Id")] public string? Id { get; set; }
    [JsonPropertyName("Type")] public string? Type { get; set; }
    [JsonPropertyName("Status")] public string? Status { get; set; }
    [JsonPropertyName("Name")] public string? Name { get; set; }
    [JsonPropertyName("ParentId")] public string? ParentId { get; set; }
    [JsonPropertyName("SubType")] public string? SubType { get; set; }
    [JsonPropertyName("StartTimestamp")] public double? StartTimestamp { get; set; }
    [JsonPropertyName("EndTimestamp")] public double? EndTimestamp { get; set; }
    [JsonPropertyName("StepDetails")] public WireStepDetails? StepDetails { get; set; }
    [JsonPropertyName("WaitDetails")] public WireWaitDetails? WaitDetails { get; set; }
    [JsonPropertyName("ExecutionDetails")] public WireExecutionDetails? ExecutionDetails { get; set; }
    [JsonPropertyName("CallbackDetails")] public WireCallbackDetails? CallbackDetails { get; set; }
    [JsonPropertyName("ChainedInvokeDetails")] public WireChainedInvokeDetails? ChainedInvokeDetails { get; set; }
    [JsonPropertyName("ContextDetails")] public WireContextDetails? ContextDetails { get; set; }

    // Epoch millis (store) → unix seconds (wire). rest-json timestamps are seconds.
    private static double? ToSeconds(long? millis) => millis.HasValue ? millis.Value / 1000.0 : null;

    public static WireOperation From(SdkOperation op) => new()
    {
        Id = op.Id,
        Type = op.Type,
        Status = op.Status,
        Name = op.Name,
        ParentId = op.ParentId,
        SubType = op.SubType,
        StartTimestamp = ToSeconds(op.StartTimestamp),
        EndTimestamp = ToSeconds(op.EndTimestamp),
        StepDetails = op.StepDetails is null ? null : new WireStepDetails
        {
            Result = op.StepDetails.Result,
            Error = WireErrorObject.From(op.StepDetails.Error),
            Attempt = op.StepDetails.Attempt,
            NextAttemptTimestamp = ToSeconds(op.StepDetails.NextAttemptTimestamp)
        },
        WaitDetails = op.WaitDetails is null ? null : new WireWaitDetails
        {
            ScheduledEndTimestamp = ToSeconds(op.WaitDetails.ScheduledEndTimestamp)
        },
        ExecutionDetails = op.ExecutionDetails is null ? null : new WireExecutionDetails
        {
            InputPayload = op.ExecutionDetails.InputPayload
        },
        CallbackDetails = op.CallbackDetails is null ? null : new WireCallbackDetails
        {
            CallbackId = op.CallbackDetails.CallbackId,
            Result = op.CallbackDetails.Result,
            Error = WireErrorObject.From(op.CallbackDetails.Error)
        },
        ChainedInvokeDetails = op.ChainedInvokeDetails is null ? null : new WireChainedInvokeDetails
        {
            Result = op.ChainedInvokeDetails.Result,
            Error = WireErrorObject.From(op.ChainedInvokeDetails.Error)
        },
        ContextDetails = op.ContextDetails is null ? null : new WireContextDetails
        {
            Result = op.ContextDetails.Result,
            Error = WireErrorObject.From(op.ContextDetails.Error),
            ReplayChildren = op.ContextDetails.ReplayChildren
        }
    };
}

internal sealed class WireStepDetails
{
    [JsonPropertyName("Result")] public string? Result { get; set; }
    [JsonPropertyName("Error")] public WireErrorObject? Error { get; set; }
    [JsonPropertyName("Attempt")] public int? Attempt { get; set; }
    [JsonPropertyName("NextAttemptTimestamp")] public double? NextAttemptTimestamp { get; set; }
}

internal sealed class WireWaitDetails
{
    [JsonPropertyName("ScheduledEndTimestamp")] public double? ScheduledEndTimestamp { get; set; }
}

internal sealed class WireExecutionDetails
{
    [JsonPropertyName("InputPayload")] public string? InputPayload { get; set; }
}

internal sealed class WireCallbackDetails
{
    [JsonPropertyName("CallbackId")] public string? CallbackId { get; set; }
    [JsonPropertyName("Result")] public string? Result { get; set; }
    [JsonPropertyName("Error")] public WireErrorObject? Error { get; set; }
}

internal sealed class WireChainedInvokeDetails
{
    [JsonPropertyName("Result")] public string? Result { get; set; }
    [JsonPropertyName("Error")] public WireErrorObject? Error { get; set; }
}

internal sealed class WireContextDetails
{
    [JsonPropertyName("Result")] public string? Result { get; set; }
    [JsonPropertyName("Error")] public WireErrorObject? Error { get; set; }
    [JsonPropertyName("ReplayChildren")] public bool? ReplayChildren { get; set; }
}
