using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Default <see cref="ICheckpointSerializer{T}"/> backed by reflection-based
/// <see cref="JsonSerializer"/>. Constructed only by the reflection-overload
/// path of <c>DurableContext.StepAsync</c>; the constructor carries
/// <see cref="RequiresUnreferencedCodeAttribute"/> so AOT/trimmed deployments
/// see the warning at the call site that picks this overload.
/// </summary>
internal sealed class ReflectionJsonCheckpointSerializer<T> : ICheckpointSerializer<T>
{
    [RequiresUnreferencedCode("Uses reflection-based JsonSerializer<T>; not AOT-safe.")]
    [RequiresDynamicCode("Uses reflection-based JsonSerializer<T>; not AOT-safe.")]
    public ReflectionJsonCheckpointSerializer() { }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Reflection-based JsonSerializer call is acknowledged on the constructor.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Reflection-based JsonSerializer call is acknowledged on the constructor.")]
    public string Serialize(T value, SerializationContext context)
    {
        return JsonSerializer.Serialize(value);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Reflection-based JsonSerializer call is acknowledged on the constructor.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Reflection-based JsonSerializer call is acknowledged on the constructor.")]
    public T Deserialize(string data, SerializationContext context)
    {
        return JsonSerializer.Deserialize<T>(data)!;
    }
}
