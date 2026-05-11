using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// One operation in the durable execution service's invocation envelope.
/// Property names mirror the wire format exactly so System.Text.Json can
/// populate this type declaratively. Internal — consumed by ExecutionState
/// and DurableContext during replay; never exposed on a public surface.
/// </summary>
internal sealed class Operation
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    [JsonPropertyName("Status")]
    public string? Status { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("ParentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("SubType")]
    public string? SubType { get; set; }

    [JsonPropertyName("StartTimestamp")]
    public long? StartTimestamp { get; set; }

    [JsonPropertyName("EndTimestamp")]
    public long? EndTimestamp { get; set; }

    [JsonPropertyName("StepDetails")]
    public StepDetails? StepDetails { get; set; }

    [JsonPropertyName("WaitDetails")]
    public WaitDetails? WaitDetails { get; set; }

    [JsonPropertyName("ExecutionDetails")]
    public ExecutionDetails? ExecutionDetails { get; set; }

    [JsonPropertyName("CallbackDetails")]
    public CallbackDetails? CallbackDetails { get; set; }

    [JsonPropertyName("ChainedInvokeDetails")]
    public ChainedInvokeDetails? ChainedInvokeDetails { get; set; }

    [JsonPropertyName("ContextDetails")]
    public ContextDetails? ContextDetails { get; set; }
}

internal sealed class StepDetails
{
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }

    [JsonPropertyName("Attempt")]
    public int? Attempt { get; set; }

    [JsonPropertyName("NextAttemptTimestamp")]
    public long? NextAttemptTimestamp { get; set; }
}

internal sealed class WaitDetails
{
    [JsonPropertyName("ScheduledEndTimestamp")]
    public long? ScheduledEndTimestamp { get; set; }
}

internal sealed class ExecutionDetails
{
    [JsonPropertyName("InputPayload")]
    public string? InputPayload { get; set; }
}

internal sealed class CallbackDetails
{
    [JsonPropertyName("CallbackId")]
    public string? CallbackId { get; set; }

    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}

internal sealed class ChainedInvokeDetails
{
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}

internal sealed class ContextDetails
{
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}

/// <summary>
/// Wire-format <see cref="Operation.Type"/> string constants.
/// Plural name avoids collision with <c>Amazon.Lambda.OperationType</c>.
/// </summary>
internal static class OperationTypes
{
    public const string Step = "STEP";
    public const string Wait = "WAIT";
    public const string Callback = "CALLBACK";
    public const string ChainedInvoke = "CHAINED_INVOKE";
    public const string Context = "CONTEXT";
    public const string Execution = "EXECUTION";
}

/// <summary>
/// Wire-format <see cref="Operation.Status"/> string constants.
/// Plural name avoids collision with <c>Amazon.Lambda.OperationStatus</c>.
/// </summary>
internal static class OperationStatuses
{
    public const string Started = "STARTED";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Pending = "PENDING";
    public const string Cancelled = "CANCELLED";
    public const string Ready = "READY";
    public const string Stopped = "STOPPED";
}
