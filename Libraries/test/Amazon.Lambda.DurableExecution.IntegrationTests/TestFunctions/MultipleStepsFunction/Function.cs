using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

public class Function
{
    private static readonly DurableEntryPoint<TestEvent, TestResult> _entry = new(Workflow);

    public static async Task Main()
    {
        await LambdaBootstrapBuilder
            .Create(_entry.InvokeAsync, new DefaultLambdaJsonSerializer())
            .Build()
            .RunAsync();
    }

    private static async Task<TestResult> Workflow(TestEvent input, IDurableContext context)
    {
        var step1 = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"a-{input.OrderId}"; },
            name: "step_1");

        var step2 = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"{step1}-b"; },
            name: "step_2");

        var step3 = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"{step2}-c"; },
            name: "step_3");

        var step4 = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"{step3}-d"; },
            name: "step_4");

        var step5 = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"{step4}-e"; },
            name: "step_5");

        return new TestResult { Status = "completed", Data = step5 };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
