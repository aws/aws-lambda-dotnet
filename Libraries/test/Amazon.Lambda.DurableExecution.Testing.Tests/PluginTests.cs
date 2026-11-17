// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class PluginTests
{
    private sealed class Recorder : IDurableInstrumentationPlugin
    {
        private readonly List<string> _events;
        public Recorder(List<string> events) => _events = events;

        public void OnInvocationStart(InvocationInfo info) { lock (_events) _events.Add($"start first={info.IsFirstInvocation}"); }
        public Task OnInvocationEndAsync(InvocationEndInfo info, CancellationToken ct)
        {
            lock (_events) _events.Add($"end {info.Status}");
            return Task.CompletedTask;
        }
        public void OnOperationEnd(OperationEndInfo info) { lock (_events) _events.Add($"op {info.Name} replay={info.IsReplay}"); }
    }

    private sealed class RecorderFactory : IDurableInstrumentationPluginFactory
    {
        private readonly List<string> _events;
        public int Created;
        public RecorderFactory(List<string> events) => _events = events;
        public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info)
        {
            Interlocked.Increment(ref Created);
            return new Recorder(_events);
        }
    }

    [Fact]
    public async Task ExecutionOptions_RunPluginsOnEveryInvocationOfTheRun()
    {
        var events = new List<string>();
        var factory = new RecorderFactory(events);
        var options = new DurableExecutionOptions();
        options.Plugins.Add(factory);

        await using var runner = new DurableTestRunner<string, string>(
            async (_, ctx) =>
            {
                await ctx.StepAsync((_, _) => Task.FromResult("a"), name: "first");
                await ctx.WaitAsync(TimeSpan.FromSeconds(5), name: "pause");
                await ctx.StepAsync((_, _) => Task.FromResult("b"), name: "second");
                return "done";
            },
            new TestRunnerOptions { ExecutionOptions = options });

        var result = await runner.RunAsync("in");

        result.EnsureSucceeded();
        Assert.Equal(2, result.InvocationCount);
        Assert.Equal(2, factory.Created);
        Assert.Equal(
            string.Join(" | ", new[]
            {
                "start first=True", "op first replay=False", "end Pending",
                "start first=False", "op first replay=True", "op pause replay=True", "op second replay=False", "end Succeeded",
            }),
            string.Join(" | ", events));
    }

    [Fact]
    public async Task WithoutExecutionOptions_NoPluginRuns()
    {
        await using var runner = new DurableTestRunner<string, string>(
            async (_, ctx) => await ctx.StepAsync((_, _) => Task.FromResult("a"), name: "only"));

        var result = await runner.RunAsync("in");

        result.EnsureSucceeded();
    }
}
