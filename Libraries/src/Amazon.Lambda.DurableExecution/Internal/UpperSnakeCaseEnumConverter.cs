using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Converts between UPPER_SNAKE_CASE wire format (e.g., CHAINED_INVOKE)
/// and PascalCase enum values (e.g., ChainedInvoke).
/// </summary>
internal sealed class UpperSnakeCaseEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        var value = reader.GetString();
        if (value == null)
            return default;

        // Convert UPPER_SNAKE_CASE to PascalCase for enum lookup
        var pascalCase = SnakeToPascal(value);

        if (Enum.TryParse<T>(pascalCase, ignoreCase: true, out var result))
            return result;

        // Fallback: try direct case-insensitive parse of the raw value
        if (Enum.TryParse<T>(value, ignoreCase: true, out result))
            return result;

        throw new JsonException($"Unable to parse '{value}' as {typeof(T).Name}.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(PascalToSnake(value.ToString()));
    }

    private static string SnakeToPascal(string snake)
    {
        var parts = snake.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i][1..].ToLower();
        }
        return string.Join("", parts);
    }

    private static string PascalToSnake(string pascal)
    {
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < pascal.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascal[i]))
                result.Append('_');
            result.Append(char.ToUpper(pascal[i]));
        }
        return result.ToString();
    }
}
