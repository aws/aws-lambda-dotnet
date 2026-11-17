// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Amazon.Lambda.DurableExecution.Instrumentation;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Amazon.Runtime;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>Invocation-level plugin wiring in <see cref="DurableFunction"/>.</summary>
public class DurableFunctionPluginTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123:durable-execution:plugin-test";

    private sealed class Recorder : IDurableInstrumentationPlugin
    {
        public InvocationInfo? Started;
        public InvocationEndInfo? Ended;
        public List<UserCodeInfo> Wraps = new();
        public List<string> Order = new();

        public void OnInvocationStart(InvocationInfo info) { Started = info; Order.Add("start"); }

        public async Task WrapAsync(UserCodeInfo info, Func<Task> next)
        {
            Wraps.Add(info);
            Order.Add("wrap.before");
            await next();
            Order.Add("wrap.after");
        }

        public Task OnInvocationEndAsync(InvocationEndInfo info, CancellationToken cancellationToken)
        {
            Ended = info;
            Order.Add("end");
            return Task.CompletedTask;
        }
    }

    private sealed class Factory : IDurableInstrumentationPluginFactory
    {
        private readonly Func<IDurableInstrumentationPlugin?> _create;
        public Factory(IDurableInstrumentationPlugin plugin, string? group = null) : this(() => plugin, group) { }
        public Factory(Func<IDurableInstrumentationPlugin?> create, string? group = null) { _create = create; ExclusiveGroup = group; }
        public string? ExclusiveGroup { get; }
        public IDurableInstrumentationPlugin? CreatePlugin(InvocationInfo info) => _create();
    }

    private sealed class ThrowingPlugin : IDurableInstrumentationPlugin
    {
        public void OnInvocationStart(InvocationInfo info) => throw new InvalidOperationException("start");
        public Task OnInvocationEndAsync(InvocationEndInfo info, CancellationToken ct) => throw new InvalidOperationException("end");
        public Task WrapAsync(UserCodeInfo info, Func<Task> next) => throw new InvalidOperationException("wrap");
    }

    private static TestLambdaContext Context(string? traceId = null) => new()
    {
        Serializer = new DefaultLambdaJsonSerializer(),
        AwsRequestId = "request-1",
        TraceId = traceId!,
    };

    private static DurableExecutionInvocationInput Input(IEnumerable<Operation>? extra = null, IReadOnlyList<string>? updated = null)
    {
        var ops = new List<Operation>
        {
            new()
            {
                Id = "exec-0",
                Type = OperationTypes.Execution,
                Status = OperationStatuses.Started,
                StartTimestamp = 1_700_000_000_000,
                ExecutionDetails = new ExecutionDetails { InputPayload = "\"in\"" },
            },
        };
        if (extra != null) ops.AddRange(extra);
        return new DurableExecutionInvocationInput
        {
            DurableExecutionArn = Arn,
            UpdatedOperationIds = updated,
            InitialExecutionState = new InitialExecutionState { Operations = ops },
        };
    }

    private static DurableExecutionOptions Options(Recorder recorder)
    {
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(recorder));
        return options;
    }

    [Fact]
    public async Task Succeeded_ReportsStartWrapAndEnd_InOrder()
    {
        var recorder = new Recorder();

        var output = await DurableFunction.WrapAsync<string, string>(
            (input, ctx) => Task.FromResult(input + "!"), Input(), Context("Root=1-abc;Parent=def;Sampled=1"),
            new MockLambdaClient(), Options(recorder));

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.Equal("\"in!\"", output.Result);
        Assert.Equal(new[] { "start", "wrap.before", "wrap.after", "end" }, recorder.Order);

        Assert.Equal(Arn, recorder.Started!.ExecutionArn);
        Assert.Equal("request-1", recorder.Started.RequestId);
        Assert.True(recorder.Started.IsFirstInvocation);
        Assert.Equal("Root=1-abc;Parent=def;Sampled=1", recorder.Started.XRayTraceId);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000), recorder.Started.ExecutionStartTimestamp);

        var wrap = Assert.Single(recorder.Wraps);
        Assert.Equal(UserCodeKind.Invocation, wrap.Kind);
        Assert.Null(wrap.Operation);

        Assert.Equal(InvocationEndStatus.Succeeded, recorder.Ended!.Status);
        Assert.Null(recorder.Ended.Error);
    }

    [Fact]
    public async Task WorkflowFailure_EndsAsFailed_WithTheError()
    {
        var recorder = new Recorder();

        var output = await DurableFunction.WrapAsync<string, string>(
            (_, _) => throw new InvalidOperationException("workflow broke"), Input(), Context(),
            new MockLambdaClient(), Options(recorder));

        Assert.Equal(InvocationStatus.Failed, output.Status);
        Assert.Equal(InvocationEndStatus.Failed, recorder.Ended!.Status);
        Assert.Equal("workflow broke", recorder.Ended.Error!.ErrorMessage);
    }

    [Fact]
    public async Task Suspension_EndsAsPending()
    {
        var recorder = new Recorder();

        var output = await DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => { await ctx.WaitAsync(TimeSpan.FromMinutes(5)); return "done"; }, Input(), Context(),
            new MockLambdaClient(), Options(recorder));

        Assert.Equal(InvocationStatus.Pending, output.Status);
        Assert.Equal(InvocationEndStatus.Pending, recorder.Ended!.Status);
    }

    [Fact]
    public async Task ThrownInvocation_EndsAsRetrying_AndRethrows()
    {
        var recorder = new Recorder();
        var client = new MockLambdaClient
        {
            // A transient service error on the final checkpoint drain escapes so Lambda retries the invocation.
            CheckpointThrows = new AmazonServiceException("unavailable") { StatusCode = HttpStatusCode.ServiceUnavailable },
        };

        await Assert.ThrowsAnyAsync<Exception>(() => DurableFunction.WrapAsync<string, string>(
            async (_, ctx) => await ctx.StepAsync((_, _) => Task.FromResult("x"), name: "s"), Input(), Context(),
            client, Options(recorder)));

        Assert.Equal(InvocationEndStatus.Retrying, recorder.Ended!.Status);
        Assert.NotNull(recorder.Ended.Exception);
    }

    [Fact]
    public async Task ResumedInvocation_ReportsHistoryAndUpdatedOperations()
    {
        var recorder = new Recorder();
        var history = new[]
        {
            new Operation { Id = "a", Type = OperationTypes.Step, Name = "first", Status = OperationStatuses.Succeeded, StepDetails = new StepDetails { Attempt = 2 } },
            new Operation { Id = "b", Type = OperationTypes.Wait, Status = OperationStatuses.Succeeded },
        };

        await DurableFunction.WrapAsync<string, string>(
            (input, _) => Task.FromResult(input), Input(history, updated: new[] { "b" }), Context(),
            new MockLambdaClient(), Options(recorder));

        var started = recorder.Started!;
        Assert.False(started.IsFirstInvocation);
        Assert.Equal(3, started.Operations.Count);
        Assert.Equal("first", started.Operations["a"].Name);
        Assert.Equal(3, started.Operations["a"].Attempt); // one more than the 2 attempts the service recorded
        var updated = Assert.Single(started.UpdatedOperations);
        Assert.Equal("b", updated.Key);
        Assert.Equal(OperationTypes.Wait, updated.Value.Type);
    }

    [Fact]
    public async Task PluginsThatThrow_DoNotAffectTheWorkflow()
    {
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(new ThrowingPlugin()));

        var output = await DurableFunction.WrapAsync<string, string>(
            (input, _) => Task.FromResult(input), Input(), Context(), new MockLambdaClient(), options);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
    }

    [Fact]
    public async Task FactoryReturningNull_RunsWithoutPlugin()
    {
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(() => null));

        var output = await DurableFunction.WrapAsync<string, string>(
            (input, _) => Task.FromResult(input), Input(), Context(), new MockLambdaClient(), options);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
    }

    [Fact]
    public async Task EachInvocationGetsAFreshPluginFromTheFactory()
    {
        var created = new List<Recorder>();
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(() => { var r = new Recorder(); created.Add(r); return r; }));

        for (var i = 0; i < 3; i++)
        {
            await DurableFunction.WrapAsync<string, string>(
                (input, _) => Task.FromResult(input), Input(), Context(), new MockLambdaClient(), options);
        }

        Assert.Equal(3, created.Count);
        Assert.All(created, r => Assert.Equal(InvocationEndStatus.Succeeded, r.Ended!.Status));
    }

    [Fact]
    public async Task ConflictingExclusiveGroups_ThrowPluginLoadException()
    {
        var options = new DurableExecutionOptions();
        options.Plugins.Add(new Factory(new Recorder(), group: "view"));
        options.Plugins.Add(new Factory(new Recorder(), group: "view"));
        var ran = false;

        await Assert.ThrowsAsync<PluginLoadException>(() => DurableFunction.WrapAsync<string, string>(
            (input, _) => { ran = true; return Task.FromResult(input); }, Input(), Context(), new MockLambdaClient(), options));

        Assert.False(ran);
    }

    [Fact]
    public async Task VoidWorkflowOverload_RunsPlugins()
    {
        var recorder = new Recorder();
        var ran = false;

        var output = await DurableFunction.WrapAsync<string>(
            (_, _) => { ran = true; return Task.CompletedTask; }, Input(), Context(), new MockLambdaClient(), Options(recorder));

        Assert.True(ran);
        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.Equal(InvocationEndStatus.Succeeded, recorder.Ended!.Status);
    }

    [Fact]
    public async Task NullOptions_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => DurableFunction.WrapAsync<string, string>(
            (input, _) => Task.FromResult(input), Input(), Context(), new MockLambdaClient(), (DurableExecutionOptions)null!));
    }
}
