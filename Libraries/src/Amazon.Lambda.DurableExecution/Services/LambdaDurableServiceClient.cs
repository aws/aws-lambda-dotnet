using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Model;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;
using SdkOperation = Amazon.Lambda.Model.Operation;

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

        var response = await _lambdaClient.CheckpointDurableExecutionAsync(request, cancellationToken);
        return response.CheckpointToken;
    }

    /// <summary>
    /// Fetches additional pages of execution state when the initial state is paginated.
    /// </summary>
    public async Task<(List<Internal.Operation> Operations, string? NextMarker)> GetExecutionStateAsync(
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

        var response = await _lambdaClient.GetDurableExecutionStateAsync(request, cancellationToken);

        var operations = new List<Internal.Operation>();
        if (response.Operations != null)
        {
            foreach (var sdkOp in response.Operations)
            {
                operations.Add(MapFromSdkOperation(sdkOp));
            }
        }

        return (operations, response.NextMarker);
    }

    private static Internal.Operation MapFromSdkOperation(SdkOperation sdkOp)
    {
        return new Internal.Operation
        {
            Id = sdkOp.Id,
            Type = sdkOp.Type,
            Status = sdkOp.Status,
            Name = sdkOp.Name,
            ParentId = sdkOp.ParentId,
            SubType = sdkOp.SubType,
            StepDetails = sdkOp.StepDetails != null ? new Internal.StepDetails
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
            WaitDetails = sdkOp.WaitDetails != null ? new Internal.WaitDetails
            {
                ScheduledEndTimestamp = sdkOp.WaitDetails.ScheduledEndTimestamp.HasValue
                    ? new DateTimeOffset(sdkOp.WaitDetails.ScheduledEndTimestamp.Value, TimeSpan.Zero).ToUnixTimeMilliseconds()
                    : null
            } : null,
            ExecutionDetails = sdkOp.ExecutionDetails != null ? new Internal.ExecutionDetails
            {
                InputPayload = sdkOp.ExecutionDetails.InputPayload
            } : null
        };
    }
}
