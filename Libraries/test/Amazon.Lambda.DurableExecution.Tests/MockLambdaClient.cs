using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// A mock that subclasses AmazonLambdaClient and overrides CheckpointDurableExecutionAsync
/// to avoid real API calls. Records checkpoint requests for test assertions.
/// </summary>
internal class MockLambdaClient : AmazonLambdaClient
{
    public List<CheckpointDurableExecutionRequest> CheckpointCalls { get; } = new();
    public List<GetDurableExecutionStateRequest> GetExecutionStateCalls { get; } = new();

    /// <summary>
    /// Optional handler for <see cref="GetDurableExecutionStateAsync"/> calls. Tests
    /// that exercise the paginated-state path can set this to control the response
    /// for each page.
    /// </summary>
    public Func<GetDurableExecutionStateRequest, GetDurableExecutionStateResponse>? GetExecutionStateHandler { get; set; }

    private int _tokenCounter;

    public MockLambdaClient() : base("fake-access-key", "fake-secret-key", Amazon.RegionEndpoint.USEast1) { }

    /// <summary>
    /// Optional exception thrown by <see cref="CheckpointDurableExecutionAsync"/>. Tests
    /// that exercise checkpoint-error classification can set this to inject a specific
    /// SDK exception on the orchestration-path drain.
    /// </summary>
    public Exception? CheckpointThrows { get; set; }

    /// <summary>
    /// Optional exception thrown by <see cref="GetDurableExecutionStateAsync"/>. Tests
    /// that exercise hydration-error classification can set this to inject a specific
    /// SDK exception on the initial state-fetch path.
    /// </summary>
    public Exception? GetExecutionStateThrows { get; set; }

    public override Task<CheckpointDurableExecutionResponse> CheckpointDurableExecutionAsync(
        CheckpointDurableExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        CheckpointCalls.Add(request);
        if (CheckpointThrows != null) throw CheckpointThrows;
        return Task.FromResult(new CheckpointDurableExecutionResponse
        {
            CheckpointToken = $"token-{++_tokenCounter}"
        });
    }

    public override Task<GetDurableExecutionStateResponse> GetDurableExecutionStateAsync(
        GetDurableExecutionStateRequest request,
        CancellationToken cancellationToken = default)
    {
        GetExecutionStateCalls.Add(request);
        if (GetExecutionStateThrows != null) throw GetExecutionStateThrows;
        if (GetExecutionStateHandler != null)
        {
            return Task.FromResult(GetExecutionStateHandler(request));
        }
        return Task.FromResult(new GetDurableExecutionStateResponse());
    }
}
