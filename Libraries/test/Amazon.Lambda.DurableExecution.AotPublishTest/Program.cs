using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace Amazon.Lambda.DurableExecution.AotPublishTest;

/// <summary>
/// AOT publish smoke check. This program must publish under NativeAOT with
/// zero IL2026/IL3050 warnings (promoted to errors by the csproj).
///
/// The user-side <see cref="AotJsonContext"/> intentionally registers ONLY the
/// workflow's input/output POCOs — no <c>DurableExecutionInvocation*</c> wire types.
/// Envelope (de)serialization is owned by the library's internal context, so any
/// internal-type leak from the public API surface would cause this project to fail
/// AOT publish (CS0053 / SYSLIB1218 / SYSLIB1220).
/// </summary>
public class Program
{
    private static readonly DurableEntryPoint<OrderEvent, OrderResult> _entry = new(WorkflowAsync);

    public static async Task Main()
    {
        await LambdaBootstrapBuilder
            .Create(_entry.InvokeAsync, new SourceGeneratorLambdaJsonSerializer<AotJsonContext>())
            .Build()
            .RunAsync();
    }

    private static async Task<OrderResult> WorkflowAsync(OrderEvent input, IDurableContext context)
    {
        var validation = await context.StepAsync(
            async (_) =>
            {
                await Task.CompletedTask;
                return new ValidationResult { IsValid = true };
            },
            name: "validate");

        await context.WaitAsync(TimeSpan.FromSeconds(30), name: "delay");

        return new OrderResult { Status = validation.IsValid ? "approved" : "rejected", OrderId = input.OrderId };
    }

    public class OrderEvent
    {
        public string? OrderId { get; set; }
    }

    public class OrderResult
    {
        public string? Status { get; set; }
        public string? OrderId { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
    }
}

[JsonSerializable(typeof(Program.OrderEvent))]
[JsonSerializable(typeof(Program.OrderResult))]
[JsonSerializable(typeof(Program.ValidationResult))]
public partial class AotJsonContext : JsonSerializerContext
{
}
