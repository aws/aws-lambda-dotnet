using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for <see cref="IDurableContext.ConfigureLogger"/>. Lets users
/// swap the underlying <see cref="ILogger"/> (e.g. Serilog, AWS Lambda Powertools)
/// or disable replay-aware filtering for debugging.
/// </summary>
public sealed class LoggerConfig
{
    /// <summary>
    /// Optional <see cref="ILogger"/> to use instead of the SDK default. When
    /// null, the durable context keeps its existing inner logger.
    /// </summary>
    public ILogger? CustomLogger { get; init; }

    /// <summary>
    /// When true (default), messages are suppressed while the workflow is
    /// re-deriving prior operations from checkpointed state. Set to false to
    /// see every log line on every replay (useful for local debugging).
    /// </summary>
    public bool ModeAware { get; init; } = true;
}
