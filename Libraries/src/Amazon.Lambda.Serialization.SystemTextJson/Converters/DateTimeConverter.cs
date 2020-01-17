using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.Lambda.Serialization.SystemTextJson.Converters
{
    /// <summary>
    /// DateTime converter that handles the JSON read for deserialization might use an epoch time.
    /// </summary>
    public class DateTimeConverter : JsonConverter<DateTime>
    {
        private static readonly DateTime EPOCH_DATETIME = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Converts the value to a DateTime. If the JSON type is a number then it assumes the time is represented as 
        /// an epoch time.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="typeToConvert"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            
            if(reader.TokenType == JsonTokenType.String && reader.TryGetDateTime(out var date))
            {
                return date;
            }
            else if(reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out var intSeconds))
                {
                    return EPOCH_DATETIME.AddSeconds(intSeconds);
                }
                if (reader.TryGetDouble(out var doubleSeconds))
                {
                    return EPOCH_DATETIME.AddSeconds(doubleSeconds);
                }
            }

            throw new JsonSerializerException($"Unknown data type for DateTime: {reader.TokenType}");
        }

        /// <summary>
        /// Uses System.Text.Json's default functionality to write dates to the Serialization document.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
