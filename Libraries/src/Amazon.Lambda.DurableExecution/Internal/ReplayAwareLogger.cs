using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// <see cref="ILogger"/> decorator that suppresses messages while the workflow
/// is replaying prior operations. Reads <see cref="ExecutionState.IsReplaying"/>
/// on every call so it correctly transitions to passthrough the moment the
/// state's per-operation tracker decides we've caught up to fresh execution.
/// </summary>
/// <remarks>
/// Mirrors the suppression behavior of the Python and Java durable execution
/// SDKs: replay <see cref="Log{TState}"/> calls return without invoking the
/// inner logger. <see cref="BeginScope{TState}"/> always delegates so scopes
/// stay balanced — suppression only applies at log emission.
/// </remarks>
internal sealed class ReplayAwareLogger : ILogger
{
    private readonly ILogger _inner;
    private readonly ExecutionState _state;
    private readonly bool _modeAware;

    public ReplayAwareLogger(ILogger inner, ExecutionState state, bool modeAware)
    {
        _inner = inner;
        _state = state;
        _modeAware = modeAware;
    }

    /// <summary>The wrapped logger; exposed so <c>ConfigureLogger</c> can rewrap without losing it.</summary>
    public ILogger Inner => _inner;

    /// <summary>Whether replay suppression is active.</summary>
    public bool ModeAware => _modeAware;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
    {
        if (ShouldSuppress()) return false;
        return _inner.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (ShouldSuppress()) return;
        _inner.Log(logLevel, eventId, state, exception, formatter);
    }

    private bool ShouldSuppress() => _modeAware && _state.IsReplaying;
}
