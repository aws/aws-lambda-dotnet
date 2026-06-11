// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class InvokeReplayDeterminismTest
{
    private readonly ITestOutputHelper _output;
    public InvokeReplayDeterminismTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task InvokeAsync_ReplayDeterminism_OperationIdsStableAcrossInvocations()
    {
        var (parent, downstream) = await DurableFunctionDeployment.CreateWithDownstreamAsync(
            parentTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeReplayDeterminismParentFunction"),
            downstreamTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeReplayDeterminismChildFunction"),
            scenarioSuffix: "invokerply",
            output: _output);

        await using (downstream)
        await using (parent)
        {
            var (invokeResponse, executionName) = await parent.InvokeAsync("""{"orderId": "invoke-replay"}""");
            var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
            _output.WriteLine($"Parent response: {responsePayload}");

            var arn = await parent.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
            Assert.NotNull(arn);

            var status = await parent.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(180));
            Assert.Equal("SUCCEEDED", status, ignoreCase: true);

            // History is eventually consistent — wait until both step-succeeded
            // events AND the chained-invoke-succeeded event are visible.
            var history = await parent.WaitForHistoryAsync(
                arn!,
                h => (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 2
                  && (h.Events?.Any(e => e.ChainedInvokeSucceededDetails != null) ?? false),
                TimeSpan.FromSeconds(60));
            var events = history.Events ?? new List<Event>();

            // Each step ran exactly once across the entire workflow — proves
            // the chained invoke's suspend/resume cycle did NOT cause the
            // pre-invoke step to re-execute. (Replay returned the cached
            // checkpoint instead.)
            var stepSucceededByName = events
                .Where(e => e.StepSucceededDetails != null)
                .GroupBy(e => e.Name)
                .ToDictionary(g => g.Key!, g => g.Count());
            Assert.Equal(1, stepSucceededByName["before_invoke"]);
            Assert.Equal(1, stepSucceededByName["after_invoke"]);

            // Exactly ONE chained invoke fired — replay didn't double-fire
            // the InvokeAsync. Same invariant we check for steps.
            Assert.Equal(1, events.Count(e => e.EventType == EventType.ChainedInvokeStarted));
            Assert.Equal(1, events.Count(e => e.ChainedInvokeSucceededDetails != null));

            var beforeInvokeEvent = events.First(e => e.StepSucceededDetails != null && e.Name == "before_invoke");
            var generatedGuid = beforeInvokeEvent.StepSucceededDetails.Result?.Payload?.Trim('"');
            Assert.NotNull(generatedGuid);
            Assert.True(Guid.TryParse(generatedGuid, out _),
                $"before_invoke should produce a valid GUID, got: {generatedGuid}");

            // The downstream's echo carries through to after_invoke verbatim,
            // proving the cached chained-invoke result was used on resume.
            var chainedSucceeded = events.First(e => e.ChainedInvokeSucceededDetails != null);
            var chainedPayload = chainedSucceeded.ChainedInvokeSucceededDetails.Result?.Payload?.Trim('"');
            Assert.Equal($"echoed:{generatedGuid}", chainedPayload);

            var afterInvokeEvent = events.First(e => e.StepSucceededDetails != null && e.Name == "after_invoke");
            var afterPayload = afterInvokeEvent.StepSucceededDetails.Result?.Payload?.Trim('"');
            Assert.Equal($"final:echoed:{generatedGuid}", afterPayload);

            // The chained invoke's suspend/resume forced at least 2 invocations
            // of the parent — proves replay actually happened (not just a
            // single straight-through execution that skipped suspension).
            var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
            Assert.True(
                invocations.Count >= 2,
                $"Expected at least 2 InvocationCompleted events (proves replay happened), got {invocations.Count}");

            // Operation IDs are stable across all replays of the same logical
            // position. The Started event and the corresponding Succeeded event
            // for each operation share the same ID — that's the clearest
            // observable proof the SDK's deterministic ID generator is working.
            // The SDK hashes <c>"&lt;counter&gt;"</c> at the root, so each ID is a
            // 64-char lowercase hex SHA-256 digest.
            var startedIds = events
                .Where(e => e.EventType == EventType.StepStarted || e.EventType == EventType.ChainedInvokeStarted)
                .Select(e => (e.Name, Id: e.Id))
                .ToList();
            var succeededIds = events
                .Where(e => e.StepSucceededDetails != null || e.ChainedInvokeSucceededDetails != null)
                .Select(e => (e.Name, Id: e.Id))
                .ToList();

            // All operation IDs are populated and look like SHA-256 hex digests.
            foreach (var (name, id) in startedIds)
            {
                Assert.False(string.IsNullOrEmpty(id), $"Operation '{name}' has no Id on its Started event");
                Assert.Equal(64, id!.Length);
                Assert.Matches("^[0-9a-f]{64}$", id);
            }

            // Every started operation ID must appear in a succeeded event —
            // proves the deterministic IDs from the Start path matched the IDs
            // the service used to record the terminal event.
            foreach (var (name, id) in startedIds)
            {
                Assert.True(
                    succeededIds.Any(s => s.Name == name && s.Id == id),
                    $"Operation '{name}' (id={id}) started but did not produce a matching SUCCEEDED event with the same ID");
            }
        }
    }
}
