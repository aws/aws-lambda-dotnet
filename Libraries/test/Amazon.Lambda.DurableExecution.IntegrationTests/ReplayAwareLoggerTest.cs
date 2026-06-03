using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// End-to-end proof of the replay-aware logger: a workflow with a Wait between
/// two steps re-invokes Lambda once. Lines emitted via
/// <c>context.Logger.LogInformation</c> in the workflow body and after step 1
/// must appear ONCE in CloudWatch (suppressed on the replay invocation),
/// while parallel <c>Console.WriteLine</c> control lines must appear TWICE
/// (proving the function genuinely replayed).
/// </summary>
public class ReplayAwareLoggerTest
{
    private readonly ITestOutputHelper _output;
    public ReplayAwareLoggerTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ReplayAwareLogger_SuppressesDuplicateLogsOnReplay()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ReplayAwareLoggerFunction"),
            "logreplay", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "log-replay"}""");
        Assert.Equal(200, invokeResponse.StatusCode);

        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // Sanity check the durable history: two step events, one wait, one
        // re-invocation. Confirms the workflow really did replay.
        await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.StepStarted) ?? 0) >= 2
              && (h.Events?.Any(e => e.WaitSucceededDetails != null) ?? false),
            TimeSpan.FromSeconds(60));

        // CloudWatch is eventually consistent — wait until ALL log lines we
        // expect have been ingested. The stop condition demands the full
        // expected count of every marker so the test never short-circuits with
        // a still-arriving "after_step1" record (which is emitted at a
        // different timestamp than workflow_start and indexes independently).
        using var logs = new AmazonCloudWatchLogsClient(RegionEndpoint.USEast1);
        var logGroup = $"/aws/lambda/{deployment.FunctionName}";

        var allEvents = await PollForLogEvents(
            logs, logGroup,
            stopWhen: events =>
                // Replay-aware: 1 each (suppressed on the second invocation).
                CountMatching(events, "LOG_REPLAY_TEST workflow_start") >= 1 &&
                CountMatching(events, "LOG_REPLAY_TEST inside_step1") >= 1 &&
                CountMatching(events, "LOG_REPLAY_TEST after_step1") >= 1 &&
                CountMatching(events, "LOG_REPLAY_TEST workflow_end") >= 1 &&
                // Control: workflow_start and after_step1 emit on both
                // invocations (2 each); workflow_end only on the second (1).
                CountMatching(events, "LOG_REPLAY_CONTROL workflow_start") >= 2 &&
                CountMatching(events, "LOG_REPLAY_CONTROL after_step1") >= 2 &&
                CountMatching(events, "LOG_REPLAY_CONTROL workflow_end") >= 1,
            timeout: TimeSpan.FromMinutes(2));

        var messages = allEvents.Select(e => e.Message ?? string.Empty).ToList();
        _output.WriteLine($"Collected {messages.Count} log events from {logGroup}");

        // Replay-aware lines: each must appear exactly once across both invocations.
        Assert.Equal(1, CountMatching(messages, "LOG_REPLAY_TEST workflow_start"));
        Assert.Equal(1, CountMatching(messages, "LOG_REPLAY_TEST inside_step1"));
        Assert.Equal(1, CountMatching(messages, "LOG_REPLAY_TEST after_step1"));
        Assert.Equal(1, CountMatching(messages, "LOG_REPLAY_TEST workflow_end"));

        // Control lines (Console.WriteLine, not replay-aware): the
        // workflow-start and after_step1 markers run on both invocations and
        // must appear twice; workflow_end runs only on the second invocation
        // (after the Wait completes) so it appears once.
        Assert.Equal(2, CountMatching(messages, "LOG_REPLAY_CONTROL workflow_start"));
        Assert.Equal(2, CountMatching(messages, "LOG_REPLAY_CONTROL after_step1"));
        Assert.Equal(1, CountMatching(messages, "LOG_REPLAY_CONTROL workflow_end"));

        // The function runs with AWS_LAMBDA_LOG_FORMAT=JSON, so the runtime
        // emits one JSON object per log record. The replay-aware lines were
        // emitted under DurableFunction's execution-level BeginScope; the
        // inside_step1 line was additionally inside StepOperation's per-step
        // BeginScope. LambdaCoreLogger appends the scope KVPs as named
        // placeholders, which the runtime's JSON formatter promotes to
        // top-level fields. Verify that.
        AssertScopeFieldsOnRecord(messages, "LOG_REPLAY_TEST workflow_start",
            requireExecutionScope: true, requireStepScope: false);
        AssertScopeFieldsOnRecord(messages, "LOG_REPLAY_TEST inside_step1",
            requireExecutionScope: true, requireStepScope: true);
    }

    private void AssertScopeFieldsOnRecord(
        List<string> messages, string substring,
        bool requireExecutionScope, bool requireStepScope)
    {
        var record = messages.FirstOrDefault(m => m.Contains(substring, StringComparison.Ordinal));
        Assert.NotNull(record);

        // CloudWatch occasionally prefixes the JSON line with text (e.g., when
        // the runtime falls back to plain stdout); slice from the first '{'.
        var braceIdx = record!.IndexOf('{');
        Assert.True(braceIdx >= 0, $"No JSON object in record: {record}");

        using var doc = JsonDocument.Parse(record[braceIdx..]);
        var root = doc.RootElement;
        _output.WriteLine($"[scope-check] {substring} → {record[braceIdx..]}");

        if (requireExecutionScope)
        {
            Assert.True(root.TryGetProperty("durableExecutionArn", out _),
                $"durableExecutionArn missing on record: {record}");
            Assert.True(root.TryGetProperty("awsRequestId", out _),
                $"awsRequestId missing on record: {record}");
        }
        if (requireStepScope)
        {
            Assert.True(root.TryGetProperty("operationId", out _),
                $"operationId missing on record: {record}");
            Assert.True(root.TryGetProperty("operationName", out _),
                $"operationName missing on record: {record}");
            Assert.True(root.TryGetProperty("attempt", out _),
                $"attempt missing on record: {record}");
        }
    }

    private static int CountMatching(IEnumerable<FilteredLogEvent> events, string substring)
        => events.Count(e => e.Message != null && e.Message.Contains(substring, StringComparison.Ordinal));

    private static int CountMatching(IEnumerable<string> messages, string substring)
        => messages.Count(m => m.Contains(substring, StringComparison.Ordinal));

    private async Task<List<FilteredLogEvent>> PollForLogEvents(
        IAmazonCloudWatchLogs logs,
        string logGroupName,
        Func<List<FilteredLogEvent>, bool> stopWhen,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var attempt = 0;
        var lastSeen = new List<FilteredLogEvent>();

        // Filter only on our marker prefix to keep payload size small.
        const string filterPattern = "\"LOG_REPLAY_\"";

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            try
            {
                var events = new List<FilteredLogEvent>();
                string? nextToken = null;
                do
                {
                    var resp = await logs.FilterLogEventsAsync(new FilterLogEventsRequest
                    {
                        LogGroupName = logGroupName,
                        FilterPattern = filterPattern,
                        NextToken = nextToken,
                    });
                    if (resp.Events != null) events.AddRange(resp.Events);
                    nextToken = resp.NextToken;
                } while (!string.IsNullOrEmpty(nextToken));

                _output.WriteLine($"[CW poll {attempt}] events={events.Count}");
                lastSeen = events;
                if (stopWhen(events)) return events;
            }
            catch (Amazon.CloudWatchLogs.Model.ResourceNotFoundException)
            {
                // Log group not yet provisioned — Lambda creates it on first
                // invocation, but it can lag behind the function being Active.
                _output.WriteLine($"[CW poll {attempt}] log group not yet present: {logGroupName}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[CW poll {attempt}] error (will retry): {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        _output.WriteLine($"[CW poll] gave up after {attempt} attempts; returning last-seen ({lastSeen.Count} events)");
        return lastSeen;
    }
}
