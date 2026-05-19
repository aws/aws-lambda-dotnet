using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution.Internal;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// The service envelope input for a durable execution invocation.
/// <see cref="DurableEntryPoint{TInput,TOutput}"/> owns (de)serialization
/// end-to-end so users only register their own POCO types.
/// </summary>
internal sealed class DurableExecutionInvocationInput
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
    /// Previously checkpointed operation state for replay. Declared <c>public</c>
    /// so <see cref="Internal.DurableEnvelopeJsonContext"/> emits a setter — STJ
    /// source-gen reads declared accessibility, not effective accessibility, and
    /// silently skips <c>internal</c>-declared members even within the same assembly.
    /// </summary>
    [JsonPropertyName("InitialExecutionState")]
    public InitialExecutionState? InitialExecutionState { get; set; }
}

/// <summary>
/// The previously checkpointed execution state provided on replay invocations.
/// </summary>
internal sealed class InitialExecutionState
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
