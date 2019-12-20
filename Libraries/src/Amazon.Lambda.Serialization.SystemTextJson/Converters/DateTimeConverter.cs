using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.Lambda.Serialization.SystemTextJson.Converters
{
    public class DateTimeConverter : JsonConverter<DateTime>
    {
        private static readonly DateTime EPOCH_DATETIME = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

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

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
