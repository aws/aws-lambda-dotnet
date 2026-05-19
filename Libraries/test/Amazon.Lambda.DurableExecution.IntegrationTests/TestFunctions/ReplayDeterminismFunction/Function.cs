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
        // Step 1 generates a fresh GUID. On replay, this MUST return the cached value.
        var generatedId = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return Guid.NewGuid().ToString(); },
            name: "generate_id");

        // Force a suspend/resume cycle to trigger replay
        await context.WaitAsync(TimeSpan.FromSeconds(3), name: "boundary_wait");

        // Step 2 echoes the GUID. After replay, it should see the SAME GUID from step 1.
        var echoed = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"echo:{generatedId}"; },
            name: "echo_id");

        return new TestResult { Status = "completed", Data = echoed };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
