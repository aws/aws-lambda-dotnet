using Amazon.Lambda.DurableExecution.Internal;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests.Internal;

public class ReplayAwareLoggerTests
{
    private const string SeedOpId = "seed";

    private static ExecutionState ReplayState()
    {
        // Seed one completed user-replayable op so IsReplaying starts true.
        // The op is NOT yet visited via TrackReplay, so we stay in replay.
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new() { Id = SeedOpId, Type = OperationTypes.Step, Status = OperationStatuses.Succeeded }
            }
        });
        Assert.True(state.IsReplaying);
        return state;
    }

    [Fact]
    public void Log_DuringReplay_Suppressed()
    {
        var inner = new RecordingLogger();
        var logger = new ReplayAwareLogger(inner, ReplayState(), modeAware: true);

        logger.LogInformation("hello");

        Assert.Empty(inner.Records);
    }

    [Fact]
    public void Log_DuringExecution_Passthrough()
    {
        var state = ReplayState();
        state.TrackReplay(SeedOpId);
        var inner = new RecordingLogger();
        var logger = new ReplayAwareLogger(inner, state, modeAware: true);

        logger.LogInformation("hello");

        Assert.Single(inner.Records);
        Assert.Equal(LogLevel.Information, inner.Records[0].Level);
    }

    [Fact]
    public void Log_ModeAwareFalse_AlwaysLogs()
    {
        var inner = new RecordingLogger();
        var logger = new ReplayAwareLogger(inner, ReplayState(), modeAware: false);

        logger.LogWarning("still here");

        Assert.Single(inner.Records);
    }

    [Fact]
    public void IsEnabled_DuringReplay_ReturnsFalse()
    {
        var inner = new RecordingLogger { ForcedEnabled = true };
        var logger = new ReplayAwareLogger(inner, ReplayState(), modeAware: true);

        Assert.False(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void IsEnabled_DuringExecution_DelegatesToInner()
    {
        var state = ReplayState();
        state.TrackReplay(SeedOpId);
        var inner = new RecordingLogger { ForcedEnabled = false };
        var logger = new ReplayAwareLogger(inner, state, modeAware: true);

        Assert.False(logger.IsEnabled(LogLevel.Information));

        inner.ForcedEnabled = true;
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void BeginScope_AlwaysDelegates()
    {
        var inner = new RecordingLogger();
        var logger = new ReplayAwareLogger(inner, ReplayState(), modeAware: true);

        // Even during replay, scopes must pass through to keep the scope stack
        // balanced.
        using (logger.BeginScope("scope-during-replay"))
        {
            Assert.Equal(1, inner.OpenScopes);
        }
        Assert.Equal(0, inner.OpenScopes);
    }

    [Fact]
    public void Log_TransitionsFromReplayToExecution()
    {
        // Mirror Python's test_logger_replay_then_new_logging: while the state
        // is replaying the logger drops messages, but the moment TrackReplay
        // visits the last checkpointed op IsReplaying flips and the next log
        // line lands.
        var state = ReplayState();
        var inner = new RecordingLogger();
        var logger = new ReplayAwareLogger(inner, state, modeAware: true);

        logger.LogInformation("during replay");
        Assert.Empty(inner.Records);

        state.TrackReplay(SeedOpId);
        logger.LogInformation("after transition");

        Assert.Single(inner.Records);
        Assert.Contains("after transition", inner.Records[0].Message);
    }
}

internal sealed class RecordingLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Records { get; } = new();
    public int OpenScopes { get; private set; }
    public bool ForcedEnabled { get; set; } = true;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        OpenScopes++;
        return new ScopeToken(this);
    }

    public bool IsEnabled(LogLevel logLevel) => ForcedEnabled;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Records.Add((logLevel, formatter(state, exception)));
    }

    private sealed class ScopeToken : IDisposable
    {
        private readonly RecordingLogger _owner;
        public ScopeToken(RecordingLogger owner) => _owner = owner;
        public void Dispose() => _owner.OpenScopes--;
    }
}
