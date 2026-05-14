using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// One operation in the durable execution service's invocation envelope.
/// Property names mirror the wire format exactly so System.Text.Json can
/// populate this type declaratively.
/// </summary>
public sealed class Operation
{
    /// <summary>The operation's unique identifier.</summary>
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    /// <summary>Operation type — see <see cref="OperationTypes"/>.</summary>
    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    /// <summary>Operation status — see <see cref="OperationStatuses"/>.</summary>
    [JsonPropertyName("Status")]
    public string? Status { get; set; }

    /// <summary>User-supplied operation name (e.g., the step name).</summary>
    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    /// <summary>Identifier of the parent operation, if any (used for nested contexts).</summary>
    [JsonPropertyName("ParentId")]
    public string? ParentId { get; set; }

    /// <summary>Operation sub-type, if any (e.g., for child contexts).</summary>
    [JsonPropertyName("SubType")]
    public string? SubType { get; set; }

    /// <summary>Unix-epoch milliseconds at which the operation started.</summary>
    [JsonPropertyName("StartTimestamp")]
    public long? StartTimestamp { get; set; }

    /// <summary>Unix-epoch milliseconds at which the operation ended.</summary>
    [JsonPropertyName("EndTimestamp")]
    public long? EndTimestamp { get; set; }

    /// <summary>Step-specific details (present when <see cref="Type"/> is <c>STEP</c>).</summary>
    [JsonPropertyName("StepDetails")]
    public StepDetails? StepDetails { get; set; }

    /// <summary>Wait-specific details (present when <see cref="Type"/> is <c>WAIT</c>).</summary>
    [JsonPropertyName("WaitDetails")]
    public WaitDetails? WaitDetails { get; set; }

    /// <summary>Execution-specific details (present when <see cref="Type"/> is <c>EXECUTION</c>).</summary>
    [JsonPropertyName("ExecutionDetails")]
    public ExecutionDetails? ExecutionDetails { get; set; }

    /// <summary>Callback-specific details (present when <see cref="Type"/> is <c>CALLBACK</c>).</summary>
    [JsonPropertyName("CallbackDetails")]
    public CallbackDetails? CallbackDetails { get; set; }

    /// <summary>Chained-invoke details (present when <see cref="Type"/> is <c>CHAINED_INVOKE</c>).</summary>
    [JsonPropertyName("ChainedInvokeDetails")]
    public ChainedInvokeDetails? ChainedInvokeDetails { get; set; }

    /// <summary>Child-context details (present when <see cref="Type"/> is <c>CONTEXT</c>).</summary>
    [JsonPropertyName("ContextDetails")]
    public ContextDetails? ContextDetails { get; set; }
}

/// <summary>Details for a STEP operation.</summary>
public sealed class StepDetails
{
    /// <summary>Serialized step result.</summary>
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    /// <summary>Error from the most recent attempt, if it failed.</summary>
    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }

    /// <summary>The attempt number (1-based).</summary>
    [JsonPropertyName("Attempt")]
    public int? Attempt { get; set; }

    /// <summary>Unix-epoch milliseconds at which the next retry attempt is scheduled.</summary>
    [JsonPropertyName("NextAttemptTimestamp")]
    public long? NextAttemptTimestamp { get; set; }
}

/// <summary>Details for a WAIT operation.</summary>
public sealed class WaitDetails
{
    /// <summary>Unix-epoch milliseconds at which the wait is scheduled to end.</summary>
    [JsonPropertyName("ScheduledEndTimestamp")]
    public long? ScheduledEndTimestamp { get; set; }
}

/// <summary>Details for an EXECUTION operation.</summary>
public sealed class ExecutionDetails
{
    /// <summary>The serialized user input payload for this invocation.</summary>
    [JsonPropertyName("InputPayload")]
    public string? InputPayload { get; set; }
}

/// <summary>Details for a CALLBACK operation.</summary>
public sealed class CallbackDetails
{
    /// <summary>The callback identifier returned to the external system.</summary>
    [JsonPropertyName("CallbackId")]
    public string? CallbackId { get; set; }

    /// <summary>Serialized callback result.</summary>
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    /// <summary>Error returned by the external system, if any.</summary>
    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}

/// <summary>Details for a CHAINED_INVOKE operation.</summary>
public sealed class ChainedInvokeDetails
{
    /// <summary>Serialized result from the invoked function.</summary>
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    /// <summary>Error returned by the invoked function, if any.</summary>
    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}

/// <summary>Details for a CONTEXT operation (child contexts).</summary>
public sealed class ContextDetails
{
    /// <summary>Serialized result of the child context.</summary>
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    /// <summary>Error from the child context, if any.</summary>
    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}

/// <summary>
/// Wire-format <see cref="Operation.Type"/> string constants.
/// Plural name avoids collision with <c>Amazon.Lambda.OperationType</c>.
/// </summary>
public static class OperationTypes
{
    /// <summary>Step operation.</summary>
    public const string Step = "STEP";

    /// <summary>Wait/timer operation.</summary>
    public const string Wait = "WAIT";

    /// <summary>Callback (external-system signal) operation.</summary>
    public const string Callback = "CALLBACK";

    /// <summary>Chained-invoke (durable-to-durable call) operation.</summary>
    public const string ChainedInvoke = "CHAINED_INVOKE";

    /// <summary>Child-context operation.</summary>
    public const string Context = "CONTEXT";

    /// <summary>Top-level execution operation carrying the user input payload.</summary>
    public const string Execution = "EXECUTION";
}

/// <summary>
/// Wire-format <see cref="Operation.SubType"/> string constants. SubType is a
/// finer-grained classifier sent alongside <see cref="Operation.Type"/> for
/// observability — the values are PascalCase ("Step", "Wait") and distinct
/// from the uppercase <see cref="OperationTypes"/> values.
/// </summary>
public static class OperationSubTypes
{
    /// <summary>Step sub-type.</summary>
    public const string Step = "Step";

    /// <summary>Wait sub-type.</summary>
    public const string Wait = "Wait";

    /// <summary>Callback sub-type.</summary>
    public const string Callback = "Callback";

    /// <summary>Wait-for-callback sub-type.</summary>
    public const string WaitForCallback = "WaitForCallback";

    /// <summary>Chained-invoke sub-type.</summary>
    public const string ChainedInvoke = "ChainedInvoke";

    /// <summary>Child-context sub-type.</summary>
    public const string Context = "Context";
}

/// <summary>
/// Wire-format <see cref="Operation.Status"/> string constants.
/// Plural name avoids collision with <c>Amazon.Lambda.OperationStatus</c>.
/// </summary>
public static class OperationStatuses
{
    /// <summary>The operation has started.</summary>
    public const string Started = "STARTED";

    /// <summary>The operation completed successfully.</summary>
    public const string Succeeded = "SUCCEEDED";

    /// <summary>The operation failed.</summary>
    public const string Failed = "FAILED";

    /// <summary>The operation is pending (waiting for time, callback, or invocation).</summary>
    public const string Pending = "PENDING";

    /// <summary>The operation was cancelled.</summary>
    public const string Cancelled = "CANCELLED";

    /// <summary>The operation is ready to resume.</summary>
    public const string Ready = "READY";

    /// <summary>The operation was stopped.</summary>
    public const string Stopped = "STOPPED";

    /// <summary>The operation timed out (e.g. callback or chained invoke timeout).</summary>
    public const string TimedOut = "TIMED_OUT";
}
