using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class MapMaxConcurrencyTest
{
    private readonly ITestOutputHelper _output;
    public MapMaxConcurrencyTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// 6 items, each with a 2-second durable wait, MaxConcurrency = 2. Validates
    /// the semaphore actually throttles dispatch: timestamps must cluster into
    /// waves rather than all six firing simultaneously. Timing tolerance is
    /// intentionally generous to avoid CI flakiness; the load-bearing assertion
    /// is "not all 6 ran at once".
    /// </summary>
    [Fact]
    public async Task Map_MaxConcurrency_ThrottlesItemDispatch()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapMaxConcurrencyFunction"),
            "mmaxc", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "m5"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // 3 waves x 2s waits + invocation overhead. Allow generous headroom.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(180));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        using var doc = JsonDocument.Parse(responsePayload);
        var successCount = doc.RootElement.GetProperty("SuccessCount").GetInt32();
        Assert.Equal(6, successCount);

        var timestamps = doc.RootElement.GetProperty("Timestamps")
            .EnumerateArray().Select(t => t.GetInt64()).ToList();
        Assert.Equal(6, timestamps.Count);

        var sorted = timestamps.OrderBy(t => t).ToList();
        var minTs = sorted[0];
        var relative = sorted.Select(t => t - minTs).ToList();
        _output.WriteLine($"Relative timestamps (ms): {string.Join(", ", relative)}");

        // Tolerant clustering: with MaxConcurrency=2 and 2s waits, the first wave
        // should hold ~2 items. Strict 3-wave clustering can be flaky under
        // service jitter, so we assert the weaker (still meaningful) property:
        // not all 6 items fired in the same wave.
        var firstWave = relative.Where(r => r < 1500).Count();
        Assert.True(firstWave <= 3,
            $"Expected MaxConcurrency=2 to limit the first wave to ~2 items; got {firstWave} within 1500ms of start. " +
            $"Relative timestamps: [{string.Join(", ", relative)}]");

        // The full set must span at least one wave-gap (~2s) — proving items did
        // NOT all run at once.
        var total = sorted[^1] - sorted[0];
        Assert.True(total >= 1500,
            $"Expected items to span >= 1500ms (proves throttling); got {total}ms. " +
            $"Relative timestamps: [{string.Join(", ", relative)}]");
    }
}
