using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.Lambda.KinesisEvents.Converters
{
    /// <summary>
    /// JSON converter to convert a JSON object with string keys and long values to a Dictionary&lt;string, string&gt; where the long values are converted to strings.
    /// </summary>
    public class DictionaryLongToStringJsonConverter : JsonConverter<Dictionary<string, string>>
    {
        /// <inheritdoc/>
        public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"JsonTokenType was of type {reader.TokenType}, only objects are supported.");
            }

            var dictionary = new Dictionary<string, string>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dictionary;
                }

                // Get the key.
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("JsonTokenType was not PropertyName.");
                }

                var propertyName = reader.GetString();

                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    throw new JsonException("Failed to get property name.");
                }

                // Get the value.
                reader.Read();
                var keyValue = ExtractValue(ref reader);
                dictionary.Add(propertyName, keyValue);
            }

            return dictionary;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
        {
            // For .NET 8+ use source generation for serialization to be trimming complaint
            JsonSerializer.Serialize(writer, value, typeof(Dictionary<string, string>), new DictionaryStringStringJsonSerializerContext(options));
        }

        private string ExtractValue(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out var result))
                    {
                        return result.ToString();
                    }
                    throw new JsonException($"Unable to convert '{reader.TokenType}' to long value.");
                case JsonTokenType.String: // If it is string, then use as it is.
                    return reader.GetString();
                default:
                    throw new JsonException($"'{reader.TokenType}' is not supported.");
            }
        }
    }

    /// <summary>
    /// Context used for writing converter
    /// </summary>
    [JsonSerializable(typeof(Dictionary<string, string>))]
    public partial class DictionaryStringStringJsonSerializerContext : JsonSerializerContext
    {

    }
}
