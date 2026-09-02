// 8-15: Parallel with a custom per-branch serde (ItemSerializer)
// Each branch returns a string directly ("x", "y"); the custom per-branch serializer
// wraps each value as the envelope {"wrapped": v} on serialize (the checkpointed branch
// payload) and unwraps it on deserialize, so the ordered results round-trip back to
// ["x", "y"]. MaxConcurrency=1 gives a deterministic history.
using System.IO;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelCustomSerdes;

public class Function
{
    public static async Task Main(string[] args)
    {
        var handler = new Function();
        var serializer = new DefaultLambdaJsonSerializer();
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>(handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<object?, List<string>>(Workflow, input, context);

    private async Task<List<string>> Workflow(object? input, IDurableContext context)
    {
        var result = await context.ParallelAsync<string>(
            new Func<IDurableContext, CancellationToken, Task<string>>[]
            {
                async (_, _) => { await Task.CompletedTask; return "x"; },
                async (_, _) => { await Task.CompletedTask; return "y"; },
            },
            name: "serde",
            config: new ParallelConfig
            {
                MaxConcurrency = 1,
                ItemSerializer = new WrapJsonSerializer(),
            });

        return result.GetResults().ToList();
    }
}

/// <summary>
/// A symmetric per-branch serializer: serializes a string value <c>v</c> as the JSON
/// envelope <c>{"wrapped":"v"}</c> and deserializes that envelope back to <c>v</c>.
/// Used only for per-branch results, so it only handles strings.
/// </summary>
public sealed class WrapJsonSerializer : ILambdaSerializer
{
    public T Deserialize<T>(Stream requestStream)
    {
        using var reader = new StreamReader(requestStream);
        var text = reader.ReadToEnd();
        using var doc = JsonDocument.Parse(text);
        var value = doc.RootElement.GetProperty("wrapped").GetString() ?? string.Empty;
        return (T)(object)value;
    }

    public void Serialize<T>(T response, Stream responseStream)
    {
        var value = response as string ?? response?.ToString() ?? string.Empty;
        var json = "{\"wrapped\":" + JsonSerializer.Serialize(value) + "}";
        var bytes = Encoding.UTF8.GetBytes(json);
        responseStream.Write(bytes, 0, bytes.Length);
    }
}
