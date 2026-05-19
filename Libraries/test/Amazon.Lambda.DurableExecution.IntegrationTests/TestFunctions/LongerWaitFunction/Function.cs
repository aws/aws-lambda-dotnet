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
            async (_) => { await Task.CompletedTask; return $"started-{input.OrderId}"; },
            name: "before_wait");

        await context.WaitAsync(TimeSpan.FromSeconds(15), name: "long_wait");

        var step2 = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"after_wait-{step1}"; },
            name: "after_wait");

        return new TestResult { Status = "completed", Data = step2 };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
