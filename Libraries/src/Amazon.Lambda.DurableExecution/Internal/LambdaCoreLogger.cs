using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using CoreLambdaLogger = Amazon.Lambda.Core.LambdaLogger;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Default <see cref="ILogger"/> for <see cref="DurableContext"/>. Routes log
/// records through <see cref="CoreLambdaLogger"/> so they flow into the same
/// pipeline used by the rest of the AWS Lambda for .NET runtime — the runtime
/// host installs a redirector that produces structured JSON when
/// <c>AWS_LAMBDA_LOG_FORMAT=JSON</c> and honors <c>AWS_LAMBDA_LOG_LEVEL</c>.
/// </summary>
/// <remarks>
/// In-package adapter to avoid forcing a dependency on
/// <c>Amazon.Lambda.Logging.AspNetCore</c>; users who want a richer experience
/// (Serilog, Powertools, etc.) can swap their own logger via
/// <see cref="IDurableContext.ConfigureLogger"/>.
///
/// When <c>state</c> is the standard <c>FormattedLogValues</c> produced by
/// <see cref="LoggerExtensions"/>, the original template and named arguments
/// are forwarded so the runtime's JSON formatter surfaces named placeholders
/// (<c>{OrderId}</c>) as top-level structured attributes. Mirrors the pattern
/// in <c>Amazon.Lambda.Logging.AspNetCore.LambdaILogger</c>.
///
/// <see cref="BeginScope"/> maintains an <see cref="AsyncLocal{T}"/> chain of
/// scope state. Scopes whose state is a key/value collection have each entry
/// appended to the outgoing template/args, so structured scope metadata
/// (<c>durableExecutionArn</c>, <c>operationId</c>, etc.) shows up as
/// top-level JSON fields without callers having to swap in a third-party
/// logger. Inner scopes win on key collision; explicit message arguments
/// always win over scope keys.
/// </remarks>
internal sealed class LambdaCoreLogger : ILogger
{
    private const string OriginalFormatKey = "{OriginalFormat}";

    private static readonly AsyncLocal<Scope?> CurrentScope = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        var scope = new Scope(state, CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    // Level filtering is performed by the runtime layer (AWS_LAMBDA_LOG_LEVEL).
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string? messageTemplate = null;
        var parameters = new List<object>();
        HashSet<string>? claimedKeys = null;

        if (state is IEnumerable<KeyValuePair<string, object?>> structure)
        {
            foreach (var property in structure)
            {
                if (property is { Key: OriginalFormatKey, Value: string value })
                {
                    messageTemplate = value;
                }
                else
                {
                    parameters.Add(property.Value!);
                    claimedKeys ??= new HashSet<string>(StringComparer.Ordinal);
                    claimedKeys.Add(property.Key);
                }
            }

            // No {OriginalFormat} → not a real FormattedLogValues; ignore the args
            // we collected and fall back to the formatter below.
            if (messageTemplate == null)
            {
                parameters.Clear();
                claimedKeys = null;
            }
        }

        messageTemplate ??= formatter(state, exception);

        AppendScopeAttributes(ref messageTemplate, parameters, ref claimedKeys);

        var levelName = logLevel.ToString();
        var args = parameters.Count == 0 ? Array.Empty<object>() : parameters.ToArray();
        if (exception != null)
        {
            CoreLambdaLogger.Log(levelName, exception, messageTemplate, args);
        }
        else
        {
            CoreLambdaLogger.Log(levelName, messageTemplate, args);
        }
    }

    private static void AppendScopeAttributes(
        ref string messageTemplate,
        List<object> parameters,
        ref HashSet<string>? claimedKeys)
    {
        var current = CurrentScope.Value;
        if (current == null) return;

        StringBuilder? sb = null;

        // Walk innermost → outermost so the first key seen for a given name wins
        // (mirrors how Microsoft.Extensions.Logging structured providers resolve
        // overlapping scope keys: the closest scope dominates).
        for (var s = current; s != null; s = s.Parent)
        {
            if (s.State is not IEnumerable<KeyValuePair<string, object?>> kvps) continue;
            foreach (var kvp in kvps)
            {
                // Skip {OriginalFormat} (some scope-state factories emit one).
                if (kvp.Key == OriginalFormatKey) continue;

                claimedKeys ??= new HashSet<string>(StringComparer.Ordinal);
                if (!claimedKeys.Add(kvp.Key)) continue;

                sb ??= new StringBuilder(messageTemplate);
                sb.Append(' ').Append('{').Append(kvp.Key).Append('}');
                parameters.Add(kvp.Value!);
            }
        }

        if (sb != null) messageTemplate = sb.ToString();
    }

    private sealed class Scope : IDisposable
    {
        public object State { get; }
        public Scope? Parent { get; }
        private bool _disposed;

        public Scope(object state, Scope? parent)
        {
            State = state;
            Parent = parent;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Restore the parent. Out-of-order disposal would desync the chain,
            // but that violates the using-statement contract that callers rely
            // on; we don't try to defend against it.
            CurrentScope.Value = Parent;
        }
    }
}
