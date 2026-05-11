namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Serializes and deserializes checkpoint operation results.
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
public interface ICheckpointSerializer<T>
{
    /// <summary>
    /// Serializes a value for checkpoint storage.
    /// </summary>
    string Serialize(T value, SerializationContext context);

    /// <summary>
    /// Deserializes a value from checkpoint storage.
    /// </summary>
    T Deserialize(string data, SerializationContext context);
}

/// <summary>
/// Context information available during serialization/deserialization.
/// </summary>
/// <param name="OperationId">The deterministic operation ID for this step.</param>
/// <param name="DurableExecutionArn">The ARN of the current durable execution.</param>
public record SerializationContext(string OperationId, string DurableExecutionArn);
