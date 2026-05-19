using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// Same wait-only workflow as <see cref="WaitOnlyTest"/>, but the function image is
/// built with NativeAOT (PublishAot=true, SourceGeneratorLambdaJsonSerializer). Catches
/// regressions where DurableExecution code is JIT-safe but breaks when trimmed/AOT-compiled.
/// </summary>
public class AotWaitOnlyTest
{
    private readonly ITestOutputHelper _output;
    public AotWaitOnlyTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task WaitOnly_NativeAot()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitOnlyAotFunction"),
            "waitonlyaot", _output, useDockerPublish: true);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "wait-only-aot"}""");
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

        var waitStarted = events.FirstOrDefault(e => e.WaitStartedDetails != null && e.Name == "only_wait");
        Assert.NotNull(waitStarted);
        Assert.Equal(5, waitStarted!.WaitStartedDetails.Duration);

        var waitSucceeded = events.FirstOrDefault(e => e.WaitSucceededDetails != null && e.Name == "only_wait");
        Assert.NotNull(waitSucceeded);

        Assert.Empty(events.Where(e => e.StepStartedDetails != null));

        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected at least 2 InvocationCompleted events (initial + post-wait resume), got {invocations.Count}");
    }
}
