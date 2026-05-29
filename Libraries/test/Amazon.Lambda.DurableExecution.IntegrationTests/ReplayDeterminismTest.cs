// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ReplayDeterminismTest
{
    private readonly ITestOutputHelper _output;
    public ReplayDeterminismTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ReplayDeterminism_SameGuidAcrossInvocations()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ReplayDeterminismFunction"),
            "replay", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "replay-test"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // History is eventually consistent — wait until both step-succeeded events are visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.StepStarted) ?? 0) >= 2
              && (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 2,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        Assert.Equal(2, events.Count(e => e.EventType == EventType.StepStarted));

        // Each step succeeded exactly once — generate_id was NOT re-executed on replay
        // (a duplicate would show up as two succeeded events for the same name).
        var stepSucceededEvents = events.Where(e => e.StepSucceededDetails != null).ToList();
        Assert.Equal(2, stepSucceededEvents.Count);
        Assert.Single(stepSucceededEvents.Where(e => e.Name == "generate_id"));
        Assert.Single(stepSucceededEvents.Where(e => e.Name == "echo_id"));

        var generateEvent = stepSucceededEvents.First(e => e.Name == "generate_id");
        var echoEvent = stepSucceededEvents.First(e => e.Name == "echo_id");

        var generatedGuid = generateEvent.StepSucceededDetails.Result?.Payload?.Trim('"');
        var echoedResult = echoEvent.StepSucceededDetails.Result?.Payload?.Trim('"');
        Assert.NotNull(generatedGuid);
        Assert.NotNull(echoedResult);
        Assert.True(Guid.TryParse(generatedGuid, out _),
            $"generate_id should produce a valid GUID, got: {generatedGuid}");

        // The echoed value matches the cached GUID — proves replay returned the
        // checkpointed value rather than running generate_id again.
        Assert.Equal($"echo:{generatedGuid}", echoedResult);

        // The boundary wait actually caused a suspend/resume cycle.
        var waitStarted = events.FirstOrDefault(e => e.WaitStartedDetails != null && e.Name == "boundary_wait");
        Assert.NotNull(waitStarted);
        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected at least 2 InvocationCompleted events (proves replay actually happened), got {invocations.Count}");
    }
}
