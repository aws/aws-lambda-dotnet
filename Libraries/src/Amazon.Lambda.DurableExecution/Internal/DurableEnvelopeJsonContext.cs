using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Source-generated JSON context for the durable execution wire envelope.
/// Co-located with the envelope types so the source generator can see every
/// internal type the envelope reaches (operation details, status converter) —
/// user-side contexts cannot, which is why envelope (de)serialization stays
/// inside the library and the user's serializer is only invoked for
/// <c>TInput</c>/<c>TOutput</c>.
/// </summary>
[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
internal partial class DurableEnvelopeJsonContext : JsonSerializerContext { }
