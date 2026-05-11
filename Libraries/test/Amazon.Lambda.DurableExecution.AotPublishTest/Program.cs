using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace Amazon.Lambda.DurableExecution.AotPublishTest;

/// <summary>
/// AOT publish smoke check. This program must publish under NativeAOT with
/// zero IL2026/IL3050 warnings (promoted to errors by the csproj). It uses
/// the JsonSerializerContext overload of WrapAsync.
/// </summary>
public class Program
{
    public static async Task Main()
    {
        var serializer = new SourceGeneratorLambdaJsonSerializer<AotJsonContext>();
        Func<DurableExecutionInvocationInput, ILambdaContext, Task<DurableExecutionInvocationOutput>> handler = HandlerAsync;
        await LambdaBootstrapBuilder
            .Create(handler, serializer)
            .Build()
            .RunAsync();
    }

    public static Task<DurableExecutionInvocationOutput> HandlerAsync(
        DurableExecutionInvocationInput input, ILambdaContext context) =>
        DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            WorkflowAsync, input, context, AotJsonContext.Default);

    private static async Task<OrderResult> WorkflowAsync(OrderEvent input, IDurableContext context)
    {
        var validation = await context.StepAsync(
            async (_) =>
            {
                await Task.CompletedTask;
                return new ValidationResult { IsValid = true };
            },
            new ValidationResultSerializer(),
            name: "validate");

        await context.WaitAsync(TimeSpan.FromSeconds(30), name: "delay");

        return new OrderResult { Status = validation.IsValid ? "approved" : "rejected", OrderId = input.OrderId };
    }

    private sealed class ValidationResultSerializer : ICheckpointSerializer<ValidationResult>
    {
        public string Serialize(ValidationResult value, SerializationContext ctx) =>
            System.Text.Json.JsonSerializer.Serialize(value, AotJsonContext.Default.ValidationResult);

        public ValidationResult Deserialize(string data, SerializationContext ctx) =>
            System.Text.Json.JsonSerializer.Deserialize(data, AotJsonContext.Default.ValidationResult)
                ?? new ValidationResult();
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

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(Program.OrderEvent))]
[JsonSerializable(typeof(Program.OrderResult))]
[JsonSerializable(typeof(Program.ValidationResult))]
public partial class AotJsonContext : JsonSerializerContext
{
}
