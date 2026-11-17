// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>Log scope fields from plugins and trace header propagation on chained invokes.</summary>
public class PluginLogAndPropagationTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123:durable-execution:log-propagation-test";

    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private sealed class ConfigurablePlugin : IDurableInstrumentationPlugin
    {
        public Func<PropagationInput, string?>? Propagate;
        public Func<IReadOnlyDictionary<string, object>?>? Scope;
        public readonly List<PropagationInput> PropagationRequests = new();

        public string? ProvidePropagationMetadata(PropagationInput input)
        {
            PropagationRequests.Add(input);
            return Propagate?.Invoke(input);
        }

        public IReadOnlyDictionary<string, object>? GetLogScope() => Scope?.Invoke();
    }

    private sealed class Factory : IDurableInstrumentationPluginFactory
    {
        private readonly IDurableInstrumentationPlugin _plugin;
        public Factory(IDurableInstrumentationPlugin plugin) => _plugin = plugin;
        public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) => _plugin;
    }

    /// <summary>A logger that records the scope fields in effect at each entry, as a structured provider would.</summary>
    private sealed class ScopeCapturingLogger : ILogger
    {
        private readonly AsyncLocal<List<IReadOnlyDictionary<string, object>>?> _scopes = new();
        public readonly List<(string Message, Dictionary<string, object> Scope)> Entries = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var previous = _scopes.Value;
            var next = new List<IReadOnlyDictionary<string, object>>(previous ?? new());
            if (state is IReadOnlyDictionary<string, object> fields) next.Add(fields);
            _scopes.Value = next;
            return new Restore(() => _scopes.Value = previous);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var merged = new Dictionary<string, object>();
            foreach (var scope in _scopes.Value ?? new())
                foreach (var kv in scope) merged[kv.Key] = kv.Value;
            lock (Entries) Entries.Add((formatter(state, exception), merged));
        }

        private sealed class Restore : IDisposable
        {
            private readonly Action _restore;
            public Restore(Action restore) => _restore = restore;
            public void Dispose() => _restore();
        }
    }

    private static TestLambdaContext Context() => new() { Serializer = new DefaultLambdaJsonSerializer() };

    private static DurableExecutionInvocationInput Input(params Operation[] history)
    {
        var ops = new List<Operation>
        {
            new()
            {
                Id = "exec-0",
                Type = OperationTypes.Execution,
                Status = OperationStatuses.Started,
                ExecutionDetails = new ExecutionDetails { InputPayload = "\"in\"" },
            },
        };
        ops.AddRange(history);
        return new DurableExecutionInvocationInput
        {
            DurableExecutionArn = Arn,
            InitialExecutionState = new InitialExecutionState { Operations = ops },
        };
    }

    private static DurableExecutionOptions Options(IDurableInstrumentationPlugin? plugin)
    {
        var options = new DurableExecutionOptions();
        if (plugin != null) options.Plugins.Add(new Factory(plugin));
        return options;
    }

    // ── Log scope ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LogEntries_CarryPluginScopeFields_ReadAtEachEntry()
    {
        var current = "span-1";
        var plugin = new ConfigurablePlugin { Scope = () => new Dictionary<string, object> { ["spanId"] = current } };
        var logger = new ScopeCapturingLogger();

        await DurableFunction.WrapAsync<string, string>((_, ctx) =>
        {
            ctx.ConfigureLogger(new LoggerConfig { CustomLogger = logger });
            ctx.Logger.LogInformation("first");
            current = "span-2";
            ctx.Logger.LogInformation("second");
            return Task.FromResult("done");
        }, Input(), Context(), new MockLambdaClient(), Options(plugin));

        Assert.Equal("span-1", logger.Entries.Single(e => e.Message == "first").Scope["spanId"]);
        Assert.Equal("span-2", logger.Entries.Single(e => e.Message == "second").Scope["spanId"]);
    }

    [Fact]
    public async Task LogEntries_HaveNoPluginFields_WhenNoPluginIsRegistered()
    {
        var logger = new ScopeCapturingLogger();

        await DurableFunction.WrapAsync<string, string>((_, ctx) =>
        {
            ctx.ConfigureLogger(new LoggerConfig { CustomLogger = logger });
            ctx.Logger.LogInformation("hello");
            return Task.FromResult("done");
        }, Input(), Context(), new MockLambdaClient(), Options(null));

        Assert.Empty(Assert.Single(logger.Entries).Scope);
    }

    [Fact]
    public async Task LogEntries_ArePlainWhenThePluginReturnsNullOrThrows()
    {
        var calls = 0;
        var plugin = new ConfigurablePlugin { Scope = () => { calls++; throw new InvalidOperationException("boom"); } };
        var logger = new ScopeCapturingLogger();

        await DurableFunction.WrapAsync<string, string>((_, ctx) =>
        {
            ctx.ConfigureLogger(new LoggerConfig { CustomLogger = logger });
            ctx.Logger.LogInformation("a");
            ctx.Logger.LogInformation("b");
            return Task.FromResult("done");
        }, Input(), Context(), new MockLambdaClient(), Options(plugin));

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Empty(e.Scope));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ReplayedLogEntries_AreSuppressed_AsBefore()
    {
        var plugin = new ConfigurablePlugin { Scope = () => new Dictionary<string, object> { ["spanId"] = "s" } };
        var logger = new ScopeCapturingLogger();
        var done = new Operation
        {
            Id = IdAt(1), Type = OperationTypes.Step, Name = "s", Status = OperationStatuses.Succeeded,
            StepDetails = new StepDetails { Result = "\"v\"" },
        };

        await DurableFunction.WrapAsync<string, string>(async (_, ctx) =>
        {
            ctx.ConfigureLogger(new LoggerConfig { CustomLogger = logger });
            ctx.Logger.LogInformation("during replay");
            await ctx.StepAsync((_, _) => Task.FromResult("v"), name: "s");
            ctx.Logger.LogInformation("after replay");
            return "done";
        }, Input(done), Context(), new MockLambdaClient(), Options(plugin));

        Assert.DoesNotContain(logger.Entries, e => e.Message == "during replay");
        Assert.Equal("s", logger.Entries.Single(e => e.Message == "after replay").Scope["spanId"]);
    }

    // ── Chained-invoke propagation ──────────────────────────────────────────

    private static string? TraceHeaderSent(MockLambdaClient client) =>
        client.CheckpointCalls
            .SelectMany(c => c.Updates)
            .Single(u => u.Type == OperationTypes.ChainedInvoke && u.Action == Amazon.Lambda.OperationAction.START)
            .ChainedInvokeOptions.XAmznTraceId;

    [Fact]
    public async Task ChainedInvoke_SendsThePluginsTraceHeader_AndDescribesTheCall()
    {
        var plugin = new ConfigurablePlugin { Propagate = _ => "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1" };
        var client = new MockLambdaClient();

        var output = await DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => await ctx.InvokeAsync<string, string>("other-function", "payload", name: "call"),
            Input(), Context(), client, Options(plugin));

        Assert.Equal(InvocationStatus.Pending, output.Status);
        Assert.Equal("Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1", TraceHeaderSent(client));

        var request = Assert.Single(plugin.PropagationRequests);
        Assert.Equal(Arn, request.ExecutionArn);
        Assert.Equal(IdAt(1), request.OperationId);
        Assert.Equal("other-function", request.TargetFunctionName);
        Assert.Null(request.ParentOperationId);
    }

    [Fact]
    public async Task ChainedInvoke_InsideAChildContext_ReportsTheParentOperation()
    {
        var plugin = new ConfigurablePlugin { Propagate = _ => "Root=1-a" };

        await DurableFunction.WrapAsync<string, string>(
            (_, ctx) => ctx.RunInChildContextAsync(
                async (child, _) => await child.InvokeAsync<string, string>("other", "p"), name: "outer"),
            Input(), Context(), new MockLambdaClient(), Options(plugin));

        Assert.Equal(IdAt(1), Assert.Single(plugin.PropagationRequests).ParentOperationId);
    }

    [Fact]
    public async Task ChainedInvoke_WithoutAContribution_OmitsTheHeader()
    {
        var plugin = new ConfigurablePlugin { Propagate = _ => null };
        var client = new MockLambdaClient();

        await DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => await ctx.InvokeAsync<string, string>("other", "p"),
            Input(), Context(), client, Options(plugin));

        Assert.Null(TraceHeaderSent(client));
    }

    [Fact]
    public async Task ChainedInvoke_WithoutPlugins_SendsNoHeader()
    {
        var client = new MockLambdaClient();

        await DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => await ctx.InvokeAsync<string, string>("other", "p"),
            Input(), Context(), client, Options(null));

        Assert.Null(TraceHeaderSent(client));
    }

    [Fact]
    public async Task ChainedInvoke_ThatThrowsFromThePlugin_StillStartsTheInvoke()
    {
        var plugin = new ConfigurablePlugin { Propagate = _ => throw new InvalidOperationException("boom") };
        var client = new MockLambdaClient();

        var output = await DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => await ctx.InvokeAsync<string, string>("other", "p"),
            Input(), Context(), client, Options(plugin));

        Assert.Equal(InvocationStatus.Pending, output.Status);
        Assert.Null(TraceHeaderSent(client));
    }

    [Fact]
    public async Task ChainedInvoke_AlreadyStarted_DoesNotAskThePluginAgain()
    {
        var plugin = new ConfigurablePlugin { Propagate = _ => "Root=1-a" };
        var started = new Operation
        {
            Id = IdAt(1), Type = OperationTypes.ChainedInvoke, Name = "call", Status = OperationStatuses.Started,
        };

        await DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => await ctx.InvokeAsync<string, string>("other", "p", name: "call"),
            Input(started), Context(), new MockLambdaClient(), Options(plugin));

        Assert.Empty(plugin.PropagationRequests);
    }
}
