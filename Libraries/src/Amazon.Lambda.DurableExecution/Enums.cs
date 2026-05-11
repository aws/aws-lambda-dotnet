namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// The terminal status of a durable execution invocation.
/// </summary>
public enum InvocationStatus
{
    /// <summary>The workflow completed successfully.</summary>
    Succeeded,
    /// <summary>The workflow failed with an unhandled exception.</summary>
    Failed,
    /// <summary>The workflow suspended (waiting for time, callback, or invocation).</summary>
    Pending
}
