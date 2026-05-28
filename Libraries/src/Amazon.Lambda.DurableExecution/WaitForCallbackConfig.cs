namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for the composite
/// <see cref="IDurableContext.WaitForCallbackAsync{T}(System.Func{string, IWaitForCallbackContext, System.Threading.Tasks.Task}, string?, WaitForCallbackConfig?, System.Threading.CancellationToken)"/>
/// operation. Inherits the callback's <see cref="CallbackConfig.Timeout"/> and
/// <see cref="CallbackConfig.HeartbeatTimeout"/>; adds a
/// <see cref="RetryStrategy"/> for the submitter step.
/// </summary>
public class WaitForCallbackConfig : CallbackConfig
{
    /// <summary>
    /// Retry strategy applied to the submitter step. When null (default),
    /// submitter failures are not retried — the submitter step fails terminally
    /// and surfaces as <see cref="CallbackSubmitterException"/>.
    /// </summary>
    public IRetryStrategy? RetryStrategy { get; set; }
}
