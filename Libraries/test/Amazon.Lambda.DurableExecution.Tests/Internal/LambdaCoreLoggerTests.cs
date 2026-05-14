using System.Reflection;
using Amazon.Lambda.DurableExecution.Internal;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests.Internal;

/// <summary>
/// Asserts that LambdaCoreLogger preserves the original message template and
/// named placeholder arguments when forwarding to Amazon.Lambda.Core.LambdaLogger.
/// This is the contract that lets the Lambda runtime's JSON formatter emit
/// {OrderId}-style fields as top-level structured attributes.
/// </summary>
public class LambdaCoreLoggerTests : IDisposable
{
    private readonly Action<string, string, object[]>? _originalLevelAction;
    private readonly Action<string, Exception, string, object[]>? _originalLevelAndExAction;
    // The capturing delegates are invoked from concurrent tasks in the AsyncLocal
    // test — guard list mutation with a lock.
    private readonly object _captureLock = new();
    private readonly List<(string Level, string Template, object[] Args, Exception? Exception)> _captured = new();

    public LambdaCoreLoggerTests()
    {
        _originalLevelAction = SwapLevelAction((level, template, args) =>
        {
            lock (_captureLock) _captured.Add((level, template, args, null));
        });

        _originalLevelAndExAction = SwapLevelAndExceptionAction((level, ex, template, args) =>
        {
            lock (_captureLock) _captured.Add((level, template, args, ex));
        });
    }

    public void Dispose()
    {
        if (_originalLevelAction != null) SwapLevelAction(_originalLevelAction);
        if (_originalLevelAndExAction != null) SwapLevelAndExceptionAction(_originalLevelAndExAction);
    }

    [Fact]
    public void Log_NamedPlaceholders_ForwardsTemplateAndArgs()
    {
        var logger = new LambdaCoreLogger();

        logger.LogInformation("User {OrderId} bought {Count}", "abc-123", 7);

        var entry = Assert.Single(_captured);
        Assert.Equal("Information", entry.Level);
        Assert.Equal("User {OrderId} bought {Count}", entry.Template);
        Assert.Equal(new object[] { "abc-123", 7 }, entry.Args);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void Log_NamedPlaceholdersWithException_ForwardsTemplateAndArgs()
    {
        var logger = new LambdaCoreLogger();
        var ex = new InvalidOperationException("boom");

        logger.LogError(ex, "Failed for {OrderId}", "abc-123");

        var entry = Assert.Single(_captured);
        Assert.Equal("Error", entry.Level);
        Assert.Equal("Failed for {OrderId}", entry.Template);
        Assert.Equal(new object[] { "abc-123" }, entry.Args);
        Assert.Same(ex, entry.Exception);
    }

    [Fact]
    public void Log_PlainMessage_ForwardsAsLiteralWithEmptyArgs()
    {
        var logger = new LambdaCoreLogger();

        logger.LogWarning("nothing structured here");

        var entry = Assert.Single(_captured);
        Assert.Equal("Warning", entry.Level);
        Assert.Equal("nothing structured here", entry.Template);
        Assert.Empty(entry.Args);
    }

    [Fact]
    public void Log_NonKvpState_FallsBackToFormatter()
    {
        var logger = new LambdaCoreLogger();

        // Direct ILogger.Log call with a custom TState that is NOT
        // FormattedLogValues. The formatter must be used to render the message.
        ((ILogger)logger).Log(
            LogLevel.Information,
            new EventId(0),
            state: 42,
            exception: null,
            formatter: (s, _) => $"value={s}");

        var entry = Assert.Single(_captured);
        Assert.Equal("Information", entry.Level);
        Assert.Equal("value=42", entry.Template);
        Assert.Empty(entry.Args);
    }

    [Fact]
    public void IsEnabled_None_ReturnsFalse()
    {
        var logger = new LambdaCoreLogger();
        Assert.False(logger.IsEnabled(LogLevel.None));
        Assert.True(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void Log_WithKvpScope_AppendsScopeKeysToTemplateAndArgs()
    {
        var logger = new LambdaCoreLogger();

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["operationId"] = "op-1",
            ["attempt"] = 2,
        }))
        {
            logger.LogInformation("step done {Result}", "ok");
        }

        var entry = Assert.Single(_captured);
        // The template's own placeholders come first; scope keys are appended.
        Assert.Equal("step done {Result} {operationId} {attempt}", entry.Template);
        Assert.Equal(new object[] { "ok", "op-1", 2 }, entry.Args);
    }

    [Fact]
    public void Log_WithNestedKvpScopes_InnerWinsAndOrderInnerToOuter()
    {
        var logger = new LambdaCoreLogger();

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["durableExecutionArn"] = "arn-outer",
            ["awsRequestId"] = "req-1",
        }))
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["operationId"] = "op-1",
            ["awsRequestId"] = "req-INNER-WINS",  // overrides outer
        }))
        {
            logger.LogInformation("hello {Name}", "world");
        }

        var entry = Assert.Single(_captured);
        // Inner scope keys appear before outer; the inner awsRequestId wins.
        Assert.Equal(
            "hello {Name} {operationId} {awsRequestId} {durableExecutionArn}",
            entry.Template);
        Assert.Equal(
            new object[] { "world", "op-1", "req-INNER-WINS", "arn-outer" },
            entry.Args);
    }

    [Fact]
    public void Log_MessageArgWinsOverScopeKeyWithSameName()
    {
        var logger = new LambdaCoreLogger();

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["OrderId"] = "from-scope",
        }))
        {
            logger.LogInformation("processing {OrderId}", "from-message");
        }

        var entry = Assert.Single(_captured);
        // Scope key OrderId is dropped because the explicit message arg already
        // claimed it; the runtime formatter sees only the explicit value.
        Assert.Equal("processing {OrderId}", entry.Template);
        Assert.Equal(new object[] { "from-message" }, entry.Args);
    }

    [Fact]
    public void BeginScope_PopsOnDispose_NoLeakAcrossLogCalls()
    {
        var logger = new LambdaCoreLogger();

        using (logger.BeginScope(new Dictionary<string, object> { ["scoped"] = "yes" }))
        {
            logger.LogInformation("inside");
        }
        logger.LogInformation("outside");

        Assert.Equal(2, _captured.Count);
        Assert.Equal("inside {scoped}", _captured[0].Template);
        Assert.Equal(new object[] { "yes" }, _captured[0].Args);
        // After the using-block, the scope is popped; the second log carries no
        // appended scope keys.
        Assert.Equal("outside", _captured[1].Template);
        Assert.Empty(_captured[1].Args);
    }

    [Fact]
    public async Task BeginScope_IsAsyncLocal_DoesNotLeakAcrossTasks()
    {
        var logger = new LambdaCoreLogger();
        var sibling1Captured = new List<(string Template, object[] Args)>();
        var sibling2Captured = new List<(string Template, object[] Args)>();

        var inflight = new TaskCompletionSource();

        async Task Sibling(string id, List<(string, object[])> sink)
        {
            using (logger.BeginScope(new Dictionary<string, object> { ["taskId"] = id }))
            {
                // Yield to give the other task a chance to run with its own scope.
                await Task.Yield();
                logger.LogInformation("emit");
                inflight.TrySetResult();
            }
        }

        // Replace the capture sink temporarily so we can route per-task.
        // Easiest: just inspect _captured, since the AsyncLocal scope chain is
        // what we care about — order doesn't matter.
        await Task.WhenAll(Sibling("A", sibling1Captured), Sibling("B", sibling2Captured));

        Assert.Equal(2, _captured.Count);
        var taskIds = _captured.Select(c => c.Args.Single()).OrderBy(v => v).ToArray();
        Assert.Equal(new object[] { "A", "B" }, taskIds);
        Assert.All(_captured, c => Assert.Equal("emit {taskId}", c.Template));
    }

    [Fact]
    public void BeginScope_NonKvpScope_Ignored()
    {
        var logger = new LambdaCoreLogger();

        using (logger.BeginScope("just-a-string"))
        {
            logger.LogInformation("hello");
        }

        var entry = Assert.Single(_captured);
        // String scopes don't carry keys; nothing to append.
        Assert.Equal("hello", entry.Template);
        Assert.Empty(entry.Args);
    }

    private static Action<string, string, object[]>? SwapLevelAction(Action<string, string, object[]> replacement)
    {
        var field = typeof(Amazon.Lambda.Core.LambdaLogger).GetField(
            "_loggingWithLevelAction",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var original = (Action<string, string, object[]>?)field.GetValue(null);
        field.SetValue(null, replacement);
        return original;
    }

    private static Action<string, Exception, string, object[]>? SwapLevelAndExceptionAction(
        Action<string, Exception, string, object[]> replacement)
    {
        var field = typeof(Amazon.Lambda.Core.LambdaLogger).GetField(
            "_loggingWithLevelAndExceptionAction",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var original = (Action<string, Exception, string, object[]>?)field.GetValue(null);
        field.SetValue(null, replacement);
        return original;
    }
}
