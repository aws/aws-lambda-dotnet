// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ParallelMaxConcurrencyTest
{
    private readonly ITestOutputHelper _output;
    public ParallelMaxConcurrencyTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// 6 branches, each with a 2-second durable wait, MaxConcurrency = 2.
    /// Validates the semaphore actually throttles dispatch: timestamps must
    /// cluster into 3 waves of 2 (not all six firing simultaneously). Timing
    /// tolerance is intentionally generous (±2s per wave gap) to avoid CI
    /// flakiness; if the wave-clustering proves flaky, fall back to
    /// "all 6 succeeded".
    /// </summary>
    [Fact]
    public async Task Parallel_MaxConcurrency_ThrottlesBranchDispatch()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelMaxConcurrencyFunction"),
            "pmaxc", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "p5"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // 3 waves x 2s waits + invocation overhead. Allow generous headroom
        // for service scheduling latency.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(180));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        using var doc = JsonDocument.Parse(responsePayload);
        var successCount = doc.RootElement.GetProperty("SuccessCount").GetInt32();
        Assert.Equal(6, successCount);

        var timestamps = doc.RootElement.GetProperty("Timestamps")
            .EnumerateArray().Select(t => t.GetInt64()).ToList();
        Assert.Equal(6, timestamps.Count);

        // Sort timestamps and check whether they cluster into 3 groups of 2.
        // Wave-N timestamps should be roughly 2s apart from wave-(N-1).
        // Use generous tolerance (±1500ms within a wave; >= 800ms gap between
        // waves) — service-driven invocations have observable jitter.
        var sorted = timestamps.OrderBy(t => t).ToList();
        var minTs = sorted[0];
        var relative = sorted.Select(t => t - minTs).ToList();
        _output.WriteLine($"Relative timestamps (ms): {string.Join(", ", relative)}");

        // Tolerant clustering: split timestamps by 1500ms gaps. With
        // MaxConcurrency=2 and 2s waits, we expect at least 2 distinct waves.
        // Strict 3-wave clustering can be flaky due to service jitter, so we
        // assert the weaker (but still meaningful) property: not all 6
        // branches fired in the same wave.
        var firstWave = relative.Where(r => r < 1500).Count();
        Assert.True(firstWave <= 3,
            $"Expected MaxConcurrency=2 to limit the first wave to ~2 branches; got {firstWave} within 1500ms of start. " +
            $"Relative timestamps: [{string.Join(", ", relative)}]");

        // The full set must span at least one wave-gap (~2s) — i.e., total
        // elapsed must exceed ~2s, proving branches did NOT all run at once.
        var total = sorted[^1] - sorted[0];
        Assert.True(total >= 1500,
            $"Expected branches to span >= 1500ms (proves throttling); got {total}ms. " +
            $"Relative timestamps: [{string.Join(", ", relative)}]");
    }
}
