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
        // The number of seconds from DateTime.MinValue to year 5000.
        private const long YEAR_5000_IN_SECONDS = 157753180800;

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
                    // If the time is in seconds is greater then the year 5000 it is safe to assume
                    // this is the special case of Kinesis sending the data which actually sends the time in milliseconds.
                    // https://github.com/aws/aws-lambda-dotnet/issues/839
                    if (intSeconds > YEAR_5000_IN_SECONDS)
                    {
                        return DateTime.UnixEpoch.AddMilliseconds(intSeconds);
                    }
                    else
                    {
                        return DateTime.UnixEpoch.AddSeconds(intSeconds);
                    }
                }
                if (reader.TryGetDouble(out var doubleSeconds))
                {
                    return DateTime.UnixEpoch.AddSeconds(doubleSeconds);
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
