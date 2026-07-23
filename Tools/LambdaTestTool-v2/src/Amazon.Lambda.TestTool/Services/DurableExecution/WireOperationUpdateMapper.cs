// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.LocalEmulation;

namespace Amazon.Lambda.TestTool.Services.DurableExecution;

/// <summary>
/// Maps the plain-STJ <see cref="WireOperationUpdate"/> deserialized from the HTTP data plane into
/// the transport-neutral <see cref="OperationUpdateInput"/> the shared checkpoint state machine
/// consumes. Wire updates already carry enum-like members as plain strings, so this mapper only
/// flattens the nested option groups and converts the wire error shape; the state machine stays
/// representation-agnostic.
/// </summary>
internal static class WireOperationUpdateMapper
{
    public static IReadOnlyList<OperationUpdateInput> ToInputs(IReadOnlyList<WireOperationUpdate> updates)
    {
        var result = new List<OperationUpdateInput>(updates.Count);
        foreach (var update in updates)
            result.Add(ToInput(update));
        return result;
    }

    public static OperationUpdateInput ToInput(WireOperationUpdate update) => new()
    {
        Id = update.Id,
        ParentId = update.ParentId,
        Name = update.Name,
        Type = update.Type,
        SubType = update.SubType,
        Action = update.Action,
        Payload = update.Payload,
        Error = MapWireError(update.Error),
        NextAttemptDelaySeconds = update.StepOptions?.NextAttemptDelaySeconds,
        WaitSeconds = update.WaitOptions?.WaitSeconds,
        ChainedInvokeFunctionName = update.ChainedInvokeOptions?.FunctionName,
    };

    private static ErrorObject? MapWireError(WireErrorObject? wireError)
    {
        if (wireError == null) return null;
        return new ErrorObject
        {
            ErrorType = wireError.ErrorType,
            ErrorMessage = wireError.ErrorMessage,
            ErrorData = wireError.ErrorData,
            StackTrace = wireError.StackTrace
        };
    }
}
