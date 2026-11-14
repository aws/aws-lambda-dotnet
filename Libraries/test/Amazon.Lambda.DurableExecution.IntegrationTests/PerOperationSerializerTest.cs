// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// Cloud integration test for the per-operation serializer override. Deploys
/// <c>PerOperationSerializerFunction</c>, which runs two steps returning the same record:
/// <c>default_step</c> (global serializer, PascalCase) and <c>camel_step</c>
/// (<c>StepConfig.Serializer = CamelCaseLambdaJsonSerializer</c>, camelCase). Asserts the
/// checkpointed payloads differ accordingly — proving the per-step serializer is applied to
/// exactly the configured step, end-to-end through the durable execution service.
/// </summary>
public class PerOperationSerializerTest
{
    private readonly ITestOutputHelper _output;
    public PerOperationSerializerTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task PerStepSerializer_AppliesToConfiguredStepOnly()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("PerOperationSerializerFunction"),
            "perser", _output);

        var (_, executionName) = await deployment.InvokeAsync("{}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // History is eventually consistent — wait until both step-succeeded events are indexed.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 2,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        string PayloadFor(string name) => events
            .First(e => e.StepSucceededDetails != null && e.Name == name)
            .StepSucceededDetails.Result.Payload;

        var defaultPayload = PayloadFor("default_step");
        var camelPayload = PayloadFor("camel_step");
        _output.WriteLine($"default_step payload: {defaultPayload}");
        _output.WriteLine($"camel_step payload:   {camelPayload}");

        // default_step used the global serializer (AWS naming policy → PascalCase).
        Assert.Contains("\"Message\"", defaultPayload);

        // camel_step overrode the serializer (camelCase) — applied to this step only.
        Assert.Contains("\"message\"", camelPayload);
        Assert.DoesNotContain("\"Message\"", camelPayload);
    }
}
