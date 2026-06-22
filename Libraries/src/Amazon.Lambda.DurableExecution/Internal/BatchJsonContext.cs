using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// AOT-friendly <see cref="JsonSerializerContext"/> for the internal
/// <see cref="BatchSummary"/> payload stored on a concurrent operation's parent
/// CONTEXT checkpoint (parallel or map). Only this internal type — never user T —
/// flows through here, so the source-generated metadata is sufficient.
/// </summary>
[JsonSerializable(typeof(BatchSummary))]
[JsonSerializable(typeof(BatchUnitSummary))]
[JsonSerializable(typeof(ErrorObject))]
internal sealed partial class BatchJsonContext : JsonSerializerContext
{
}
