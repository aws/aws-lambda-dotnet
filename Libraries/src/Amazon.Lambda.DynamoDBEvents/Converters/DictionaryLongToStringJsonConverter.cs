using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.Lambda.DynamoDBEvents.Converters
{
    public class DictionaryLongToStringJsonConverter : JsonConverter<Dictionary<string, string>>
    {
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
                var keyValue = ExtractValue(ref reader, options);
                dictionary.Add(propertyName, keyValue);
            }

            return dictionary;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
        {
            // Use the built-in serializer, because it can handle dictionaries with string keys.
            JsonSerializer.Serialize(writer, value, options);
        }

        private string ExtractValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
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
}