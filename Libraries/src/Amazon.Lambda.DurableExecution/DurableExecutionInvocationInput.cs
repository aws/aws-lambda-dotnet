using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// The service envelope input for a durable execution invocation.
/// This is what Lambda receives from the durable execution service.
/// </summary>
public sealed class DurableExecutionInvocationInput
{
    /// <summary>
    /// The unique ARN identifying this durable execution.
    /// </summary>
    [JsonPropertyName("DurableExecutionArn")]
    public required string DurableExecutionArn { get; set; }

    /// <summary>
    /// Token for optimistic concurrency on checkpoint operations.
    /// </summary>
    [JsonPropertyName("CheckpointToken")]
    public string? CheckpointToken { get; set; }

    /// <summary>
    /// Previously checkpointed operation state for replay. Consumed by
    /// <c>DurableFunction.WrapAsync</c> for replay correlation; user code
    /// should not modify this on a live invocation envelope.
    /// </summary>
    [JsonPropertyName("InitialExecutionState")]
    public InitialExecutionState? InitialExecutionState { get; set; }
}

/// <summary>
/// The previously checkpointed execution state provided on replay invocations.
/// </summary>
public sealed class InitialExecutionState
{
    /// <summary>
    /// The list of operations from prior invocations.
    /// </summary>
    [JsonPropertyName("Operations")]
    public IReadOnlyList<Operation>? Operations { get; set; }

    /// <summary>
    /// If present, indicates that more operations are available. Use this value
    /// with GetDurableExecutionState to fetch the next page.
    /// </summary>
    [JsonPropertyName("NextMarker")]
    public string? NextMarker { get; set; }
}
