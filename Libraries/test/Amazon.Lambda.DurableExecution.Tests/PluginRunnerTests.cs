// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.DurableExecution.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class PluginRunnerTests
{
    private static readonly InvocationInfo Info = new() { ExecutionArn = "arn:test" };
    private static readonly UserCodeInfo Code = new() { Kind = UserCodeKind.Invocation };

    private sealed class RecordingPlugin : IDurableInstrumentationPlugin
    {
        private readonly string _name;
        private readonly List<string> _log;
        public RecordingPlugin(string name, List<string> log) { _name = name; _log = log; }

        public void OnInvocationStart(InvocationInfo info) => _log.Add($"{_name}.start");
        public Task OnInvocationEndAsync(InvocationEndInfo info, CancellationToken ct)
        {
            _log.Add($"{_name}.end");
            return Task.CompletedTask;
        }

        public async Task WrapAsync(UserCodeInfo info, Func<Task> next)
        {
            _log.Add($"{_name}.wrap.before");
            await next();
            _log.Add($"{_name}.wrap.after");
        }
    }

    private sealed class LambdaPlugin : IDurableInstrumentationPlugin
    {
        public Action<InvocationInfo>? Start;
        public Func<InvocationEndInfo, CancellationToken, Task>? End;
        public Func<UserCodeInfo, Func<Task>, Task>? Wrap;
        public Func<PropagationInput, string?>? Propagate;
        public Func<IReadOnlyDictionary<string, object>?>? Scope;

        public void OnInvocationStart(InvocationInfo info) => Start?.Invoke(info);
        public Task OnInvocationEndAsync(InvocationEndInfo info, CancellationToken ct) => End?.Invoke(info, ct) ?? Task.CompletedTask;
        public Task WrapAsync(UserCodeInfo info, Func<Task> next) => Wrap?.Invoke(info, next) ?? next();
        public string? ProvidePropagationMetadata(PropagationInput input) => Propagate?.Invoke(input);
        public IReadOnlyDictionary<string, object>? GetLogScope() => Scope?.Invoke();
    }

    private sealed class Factory : IDurableInstrumentationPluginFactory
    {
        private readonly Func<IDurableInstrumentationPlugin?> _create;
        public Factory(Func<IDurableInstrumentationPlugin?> create, string? group = null) { _create = create; ExclusiveGroup = group; }
        public string? ExclusiveGroup { get; }
        public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) => _create();
    }

    private sealed class ListLogger : ILogger
    {
        public readonly List<string> Messages = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private static PluginRunner Runner(ILogger? logger = null, Func<TimeSpan?>? remaining = null, params IDurableInstrumentationPlugin[] plugins)
    {
        var options = new DurableExecutionOptions();
        foreach (var p in plugins)
        {
            var captured = p;
            options.Plugins.Add(new Factory(() => captured));
        }
        return PluginRunner.Create(options, Info, logger ?? NullLogger.Instance, remaining ?? (() => null))!;
    }

    [Fact]
    public void Create_ReturnsNull_WithoutPlugins()
    {
        Assert.Null(PluginRunner.Create(null, Info, NullLogger.Instance, () => null));
        Assert.Null(PluginRunner.Create(new DurableExecutionOptions(), Info, NullLogger.Instance, () => null));
    }

    [Fact]
    public void Create_Throws_ForTwoFactoriesInOneExclusiveGroup()
    {
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(() => new LambdaPlugin(), "view"));
        options.Plugins.Add(new Factory(() => new LambdaPlugin(), "view"));

        Assert.Throws<PluginLoadException>(() => PluginRunner.Create(options, Info, NullLogger.Instance, () => null));
    }

    [Fact]
    public void Create_AllowsDifferentOrNoGroups_AndThrowsForNullFactory()
    {
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(() => new LambdaPlugin(), "a"));
        options.Plugins.Add(new Factory(() => new LambdaPlugin(), "b"));
        options.Plugins.Add(new Factory(() => new LambdaPlugin()));
        Assert.NotNull(PluginRunner.Create(options, Info, NullLogger.Instance, () => null));

        options.Plugins.Add(null!);
        Assert.Throws<PluginLoadException>(() => PluginRunner.Create(options, Info, NullLogger.Instance, () => null));
    }

    [Fact]
    public void Create_SkipsFactoriesThatThrowOrReturnNull()
    {
        var logger = new ListLogger();
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(() => throw new InvalidOperationException("boom")));
        options.Plugins.Add(new Factory(() => null));
        var started = false;
        options.Plugins.Add(new Factory(() => new LambdaPlugin { Start = _ => started = true }));

        var runner = PluginRunner.Create(options, Info, logger, () => null)!;
        runner.InvocationStart(Info);

        Assert.True(started);
        Assert.Single(logger.Messages);
    }

    [Fact]
    public void Hooks_AreIsolated_AndLaterPluginsStillRun()
    {
        var logger = new ListLogger();
        var secondRan = false;
        var runner = Runner(logger, null,
            new LambdaPlugin { Start = _ => throw new InvalidOperationException("boom") },
            new LambdaPlugin { Start = _ => secondRan = true });

        runner.InvocationStart(Info);

        Assert.True(secondRan);
        Assert.Contains(logger.Messages, m => m.Contains("OnInvocationStart"));
    }

    [Fact]
    public async Task Ordering_StartForward_EndReverse_WrapFirstOutermost()
    {
        var log = new List<string>();
        var runner = Runner(null, null, new RecordingPlugin("A", log), new RecordingPlugin("B", log));

        runner.InvocationStart(Info);
        await runner.WrapAsync(Code, () => { log.Add("user"); return Task.CompletedTask; });
        await runner.InvocationEndAsync(new InvocationEndInfo { ExecutionArn = "arn:test", Status = InvocationEndStatus.Succeeded });

        Assert.Equal(
            new[] { "A.start", "B.start", "A.wrap.before", "B.wrap.before", "user", "B.wrap.after", "A.wrap.after", "B.end", "A.end" },
            log);
    }

    [Fact]
    public async Task Wrap_PluginThrowsBeforeNext_UserCodeStillRunsOnce()
    {
        var runs = 0;
        var runner = Runner(null, null, new LambdaPlugin { Wrap = (_, _) => throw new InvalidOperationException("boom") });

        await runner.WrapAsync(Code, () => { runs++; return Task.CompletedTask; });

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Wrap_PluginNeverCallsNext_UserCodeStillRunsOnce()
    {
        var runs = 0;
        var runner = Runner(null, null, new LambdaPlugin { Wrap = (_, _) => Task.CompletedTask });

        await runner.WrapAsync(Code, () => { runs++; return Task.CompletedTask; });

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Wrap_UserExceptionReachesCaller_EvenWhenPluginThrowsOrSwallowsIt()
    {
        var userException = new ApplicationException("user");

        var rethrowing = Runner(null, null, new LambdaPlugin
        {
            Wrap = async (_, next) => { try { await next(); } catch { throw new InvalidOperationException("plugin"); } }
        });
        Assert.Same(userException, await Assert.ThrowsAsync<ApplicationException>(
            () => rethrowing.WrapAsync(Code, () => throw userException)));

        var swallowing = Runner(null, null, new LambdaPlugin
        {
            Wrap = async (_, next) => { try { await next(); } catch { } }
        });
        Assert.Same(userException, await Assert.ThrowsAsync<ApplicationException>(
            () => swallowing.WrapAsync(Code, () => throw userException)));
    }

    [Fact]
    public async Task Wrap_PluginThrowingAfterSuccessfulUserCode_IsIgnored()
    {
        var runner = Runner(null, null, new LambdaPlugin
        {
            Wrap = async (_, next) => { await next(); throw new InvalidOperationException("plugin"); }
        });

        await runner.WrapAsync(Code, () => Task.CompletedTask);
    }

    [Fact]
    public void Propagation_FirstNonBlankWins_AndConflictsAreLoggedWithoutTheHeader()
    {
        var logger = new ListLogger();
        var input = new PropagationInput { ExecutionArn = "arn", OperationId = "op", TargetFunctionName = "fn" };
        var runner = Runner(logger, null,
            new LambdaPlugin { Propagate = _ => "  " },
            new LambdaPlugin { Propagate = _ => throw new InvalidOperationException("boom") },
            new LambdaPlugin { Propagate = _ => "Root=1-a" },
            new LambdaPlugin { Propagate = _ => "Root=1-b" },
            new LambdaPlugin { Propagate = _ => "Root=1-a" });

        Assert.Equal("Root=1-a", runner.ProvidePropagationMetadata(input));
        Assert.Equal(2, logger.Messages.Count);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("Root=1-b"));
    }

    [Fact]
    public void LogScope_MergesPlugins_FirstKeyWins()
    {
        var runner = Runner(null, null,
            new LambdaPlugin { Scope = () => new Dictionary<string, object> { ["traceId"] = "one", ["a"] = 1 } },
            new LambdaPlugin { Scope = () => throw new InvalidOperationException("boom") },
            new LambdaPlugin { Scope = () => new Dictionary<string, object> { ["traceId"] = "two", ["b"] = 2 } });

        var scope = runner.GetLogScope()!;

        Assert.Equal("one", scope["traceId"]);
        Assert.Equal(1, scope["a"]);
        Assert.Equal(2, scope["b"]);
    }

    [Fact]
    public async Task InvocationEnd_SlowPlugin_IsCancelledAndAbandonedWithinTheRemainingTime()
    {
        var cancelled = new TaskCompletionSource();
        var logger = new ListLogger();
        var neverFinishes = new LambdaPlugin
        {
            End = (_, ct) => { ct.Register(() => cancelled.TrySetResult()); return new TaskCompletionSource().Task; }
        };
        var after = false;
        var runner = Runner(logger, () => TimeSpan.FromMilliseconds(250), neverFinishes, new LambdaPlugin { End = (_, _) => { after = true; return Task.CompletedTask; } });

        // The second plugin runs first (reverse order), then the hung one is cut off.
        await runner.InvocationEndAsync(new InvocationEndInfo { ExecutionArn = "arn:test", Status = InvocationEndStatus.Succeeded });

        Assert.True(after);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(logger.Messages, m => m.Contains("abandoned"));
    }

    [Fact]
    public async Task InvocationEnd_ExceptionIsIsolated_WithAndWithoutADeadline()
    {
        foreach (Func<TimeSpan?> remaining in new Func<TimeSpan?>[] { () => null, () => TimeSpan.FromSeconds(30) })
        {
            var logger = new ListLogger();
            var runner = Runner(logger, remaining, new LambdaPlugin
            {
                End = (_, _) => throw new InvalidOperationException("sync"),
            }, new LambdaPlugin
            {
                End = async (_, _) => { await Task.Yield(); throw new InvalidOperationException("async"); },
            });

            await runner.InvocationEndAsync(new InvocationEndInfo { ExecutionArn = "arn:test", Status = InvocationEndStatus.Failed });

            Assert.Equal(2, logger.Messages.Count);
        }
    }

    [Fact]
    public async Task InvocationEnd_RemainingTimeThatThrows_MeansNoDeadline()
    {
        var done = false;
        var runner = Runner(null, () => throw new InvalidOperationException("no deadline"),
            new LambdaPlugin { End = async (_, _) => { await Task.Delay(20); done = true; } });

        await runner.InvocationEndAsync(new InvocationEndInfo { ExecutionArn = "arn:test", Status = InvocationEndStatus.Succeeded });

        Assert.True(done);
    }
}
