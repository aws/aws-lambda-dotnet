// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.LocalEmulation;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Maps the AWSSDK <c>Amazon.Lambda.Model.OperationUpdate</c> the in-process runtime emits into the
/// transport-neutral <see cref="OperationUpdateInput"/> the shared checkpoint state machine consumes.
/// This is where the SDK's <c>ConstantClass</c> enum-like members are unwrapped to plain strings and
/// nested option/error types are flattened; the state machine itself stays representation-agnostic.
/// </summary>
internal static class SdkOperationUpdateMapper
{
    public static IReadOnlyList<OperationUpdateInput> ToInputs(IReadOnlyList<SdkOperationUpdate> updates)
    {
        var result = new List<OperationUpdateInput>(updates.Count);
        foreach (var update in updates)
            result.Add(ToInput(update));
        return result;
    }

    public static OperationUpdateInput ToInput(SdkOperationUpdate update) => new()
    {
        Id = update.Id,
        ParentId = update.ParentId,
        Name = update.Name,
        Type = update.Type?.Value,
        SubType = update.SubType,
        Action = update.Action?.Value,
        Payload = update.Payload,
        Error = MapSdkError(update.Error),
        NextAttemptDelaySeconds = update.StepOptions?.NextAttemptDelaySeconds,
        WaitSeconds = update.WaitOptions?.WaitSeconds,
        ChainedInvokeFunctionName = update.ChainedInvokeOptions?.FunctionName,
    };

    private static ErrorObject? MapSdkError(Amazon.Lambda.Model.ErrorObject? sdkError)
    {
        if (sdkError == null) return null;
        return new ErrorObject
        {
            ErrorType = sdkError.ErrorType,
            ErrorMessage = sdkError.ErrorMessage,
            ErrorData = sdkError.ErrorData,
            StackTrace = sdkError.StackTrace
        };
    }
}
