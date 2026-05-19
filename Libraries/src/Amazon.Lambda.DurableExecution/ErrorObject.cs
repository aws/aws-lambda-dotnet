using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Serializable error representation stored in checkpoint state.
/// </summary>
public sealed class ErrorObject
{
    /// <summary>
    /// The fully-qualified exception type name.
    /// </summary>
    [JsonPropertyName("ErrorType")]
    public string? ErrorType { get; set; }

    /// <summary>
    /// The exception message.
    /// </summary>
    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace frames.
    /// </summary>
    [JsonPropertyName("StackTrace")]
    public IReadOnlyList<string>? StackTrace { get; set; }

    /// <summary>
    /// Additional serialized error data.
    /// </summary>
    [JsonPropertyName("ErrorData")]
    public string? ErrorData { get; set; }

    /// <summary>
    /// Creates an ErrorObject from an exception.
    /// </summary>
    public static ErrorObject FromException(Exception exception)
    {
        return new ErrorObject
        {
            ErrorType = exception.GetType().FullName,
            ErrorMessage = exception.Message,
            StackTrace = exception.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        };
    }
}
