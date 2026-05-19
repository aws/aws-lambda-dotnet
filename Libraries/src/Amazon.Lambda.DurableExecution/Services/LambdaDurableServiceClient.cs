using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;
using SdkOperation = Amazon.Lambda.Model.Operation;
using Operation = Amazon.Lambda.DurableExecution.Operation;
using StepDetails = Amazon.Lambda.DurableExecution.StepDetails;
using WaitDetails = Amazon.Lambda.DurableExecution.WaitDetails;
using ExecutionDetails = Amazon.Lambda.DurableExecution.ExecutionDetails;

namespace Amazon.Lambda.DurableExecution.Services;

/// <summary>
/// Calls the real AWS Lambda Durable Execution APIs via the AWSSDK.Lambda client.
/// </summary>
internal sealed class LambdaDurableServiceClient
{
    private readonly IAmazonLambda _lambdaClient;

    public LambdaDurableServiceClient(IAmazonLambda lambdaClient)
    {
        _lambdaClient = lambdaClient;
    }

    /// <summary>
    /// Flushes pending checkpoint operations to the durable execution service.
    /// SDK errors are wrapped in <see cref="DurableExecutionException"/> so user logs
    /// show the durable-execution context (which API call, which ARN) alongside the
    /// underlying SDK message — instead of a bare AWSSDK stack trace with no clue
    /// about what was being called.
    /// </summary>
    public async Task<string?> CheckpointAsync(
        string durableExecutionArn,
        string? checkpointToken,
        IReadOnlyList<SdkOperationUpdate> pendingOperations,
        CancellationToken cancellationToken = default)
    {
        if (pendingOperations.Count == 0)
            return checkpointToken;

        var request = new CheckpointDurableExecutionRequest
        {
            DurableExecutionArn = durableExecutionArn,
            CheckpointToken = checkpointToken ?? "",
            Updates = pendingOperations is List<SdkOperationUpdate> list ? list : pendingOperations.ToList()
        };

        try
        {
            var response = await _lambdaClient.CheckpointDurableExecutionAsync(request, cancellationToken);
            return response.CheckpointToken;
        }
        catch (AmazonServiceException ex)
        {
            throw new DurableExecutionException(
                $"Failed to checkpoint operations for durable execution '{durableExecutionArn}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Fetches additional pages of execution state when the initial state is paginated.
    /// SDK errors are wrapped in <see cref="DurableExecutionException"/> for the same
    /// reason as <see cref="CheckpointAsync"/>.
    /// </summary>
    public async Task<(List<Operation> Operations, string? NextMarker)> GetExecutionStateAsync(
        string durableExecutionArn,
        string? checkpointToken,
        string marker,
        CancellationToken cancellationToken = default)
    {
        var request = new GetDurableExecutionStateRequest
        {
            DurableExecutionArn = durableExecutionArn,
            CheckpointToken = checkpointToken ?? "",
            Marker = marker
        };

        GetDurableExecutionStateResponse response;
        try
        {
            response = await _lambdaClient.GetDurableExecutionStateAsync(request, cancellationToken);
        }
        catch (AmazonServiceException ex)
        {
            throw new DurableExecutionException(
                $"Failed to fetch execution state for durable execution '{durableExecutionArn}' (marker '{marker}'): {ex.Message}",
                ex);
        }

        var operations = new List<Operation>();
        if (response.Operations != null)
        {
            foreach (var sdkOp in response.Operations)
            {
                operations.Add(MapFromSdkOperation(sdkOp));
            }
        }

        return (operations, response.NextMarker);
    }

    private static Operation MapFromSdkOperation(SdkOperation sdkOp)
    {
        return new Operation
        {
            Id = sdkOp.Id,
            Type = sdkOp.Type,
            Status = sdkOp.Status,
            Name = sdkOp.Name,
            ParentId = sdkOp.ParentId,
            SubType = sdkOp.SubType,
            StepDetails = sdkOp.StepDetails != null ? new StepDetails
            {
                Result = sdkOp.StepDetails.Result,
                Error = sdkOp.StepDetails.Error != null ? new ErrorObject
                {
                    ErrorType = sdkOp.StepDetails.Error.ErrorType,
                    ErrorMessage = sdkOp.StepDetails.Error.ErrorMessage
                } : null,
                Attempt = sdkOp.StepDetails.Attempt,
                NextAttemptTimestamp = sdkOp.StepDetails.NextAttemptTimestamp.HasValue
                    ? new DateTimeOffset(sdkOp.StepDetails.NextAttemptTimestamp.Value, TimeSpan.Zero).ToUnixTimeMilliseconds()
                    : null
            } : null,
            WaitDetails = sdkOp.WaitDetails != null ? new WaitDetails
            {
                ScheduledEndTimestamp = sdkOp.WaitDetails.ScheduledEndTimestamp.HasValue
                    ? new DateTimeOffset(sdkOp.WaitDetails.ScheduledEndTimestamp.Value, TimeSpan.Zero).ToUnixTimeMilliseconds()
                    : null
            } : null,
            ExecutionDetails = sdkOp.ExecutionDetails != null ? new ExecutionDetails
            {
                InputPayload = sdkOp.ExecutionDetails.InputPayload
            } : null
        };
    }
}
