using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.Lambda.Serialization.SystemTextJson.Converters
{
    /// <summary>
    /// ByteArrayConverter for converting an JSON array of number from and to byte[].
    /// </summary>
    public class ByteArrayConverter : JsonConverter<byte[]>
    {
        /// <inheritdoc />
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            var byteList = new List<byte>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Number:
                        byteList.Add(reader.GetByte());
                        break;
                    case JsonTokenType.EndArray:
                        return byteList.ToArray();
                }
            }

            throw new JsonException("The JSON value could not be converted to byte[].");
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, byte[] values, JsonSerializerOptions options)
        {
            if (values == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartArray();

                foreach (var value in values)
                {
                    writer.WriteNumberValue(value);
                }

                writer.WriteEndArray();
            }
        }
    }
}
