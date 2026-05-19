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
        await context.WaitAsync(TimeSpan.FromSeconds(5), name: "only_wait");
        return new TestResult { Status = "completed", Data = "wait_only" };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
