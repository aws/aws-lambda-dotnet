using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution.Internal;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// The service envelope output returned by a durable execution invocation.
/// Written by <see cref="DurableEntryPoint{TInput,TOutput}"/> directly to the
/// Lambda response stream.
/// </summary>
internal sealed class DurableExecutionInvocationOutput
{
    /// <summary>
    /// The terminal status of this invocation.
    /// </summary>
    [JsonPropertyName("Status")]
    [JsonConverter(typeof(InvocationStatusConverter))]
    public required InvocationStatus Status { get; set; }

    /// <summary>
    /// The serialized result (only present when Status is Succeeded).
    /// </summary>
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    /// <summary>
    /// Error details (only present when Status is Failed).
    /// </summary>
    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}
