// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class WaitOnlyTest
{
    private readonly ITestOutputHelper _output;
    public WaitOnlyTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task WaitOnly_NoSteps()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitOnlyFunction"),
            "waitonly", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "wait-only"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.WaitSucceededDetails != null) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // The wait was checkpointed and ran for the configured duration.
        var waitStarted = events.FirstOrDefault(e => e.WaitStartedDetails != null && e.Name == "only_wait");
        Assert.NotNull(waitStarted);
        Assert.Equal(5, waitStarted!.WaitStartedDetails.Duration);

        var waitSucceeded = events.FirstOrDefault(e => e.WaitSucceededDetails != null && e.Name == "only_wait");
        Assert.NotNull(waitSucceeded);

        // No step events: this workflow body contains only a wait.
        Assert.Empty(events.Where(e => e.StepStartedDetails != null));

        // The wait genuinely caused a suspend/resume, not an in-process delay:
        // expect at least 2 invocations recorded (initial + resume after timer fires).
        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected at least 2 InvocationCompleted events (initial + post-wait resume), got {invocations.Count}");
    }
}
