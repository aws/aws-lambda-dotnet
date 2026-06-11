// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class LongerWaitTest
{
    private readonly ITestOutputHelper _output;
    public LongerWaitTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task LongerWait_ExpiresAndCompletes()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("LongerWaitFunction"),
            "longwait", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "long-wait-test"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(90));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.StepStarted) ?? 0) >= 2
              && (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 2
              && (h.Events?.Any(e => e.WaitSucceededDetails != null) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        Assert.Equal(2, events.Count(e => e.EventType == EventType.StepStarted));

        // Steps before and after the wait both ran, with the post-wait step seeing
        // the pre-wait step's value via replay.
        var stepResults = events
            .Where(e => e.StepSucceededDetails != null)
            .Select(e => (Name: e.Name, Payload: e.StepSucceededDetails.Result?.Payload?.Trim('"')))
            .ToList();
        Assert.Equal(2, stepResults.Count);
        Assert.Equal("before_wait", stepResults[0].Name);
        Assert.Equal("started-long-wait-test", stepResults[0].Payload);
        Assert.Equal("after_wait", stepResults[1].Name);
        Assert.Equal("after_wait-started-long-wait-test", stepResults[1].Payload);

        // The wait was checkpointed for the configured 15-second duration.
        var waitStarted = events.FirstOrDefault(e => e.WaitStartedDetails != null && e.Name == "long_wait");
        Assert.NotNull(waitStarted);
        Assert.Equal(15, waitStarted!.WaitStartedDetails.Duration);

        // The wait spanned at least two invocations: one to schedule it and at
        // least one to resume after the timer fires.
        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected at least 2 InvocationCompleted events (suspend + resume), got {invocations.Count}");
    }
}
