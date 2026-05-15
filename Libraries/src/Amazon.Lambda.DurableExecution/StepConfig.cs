namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for step execution.
/// </summary>
public sealed class StepConfig
{
    // TODO: Retry support is deferred to a follow-up PR. When added, this is
    // where RetryStrategy and Semantics (AtLeastOncePerRetry / AtMostOncePerRetry)
    // will live. The follow-up needs to use service-mediated retries (checkpoint
    // a RETRY operation + suspend the Lambda) rather than an in-process Task.Delay
    // loop, to avoid billing Lambda compute time during retry backoff.
}
