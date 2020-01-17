using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Amazon.Lambda.Serialization.SystemTextJson.Converters
{
    /// <summary>
    /// Handles converting MemoryStreams from and to base 64 strings.
    /// </summary>
    public class MemoryStreamConverter : JsonConverter<MemoryStream>
    {
        /// <summary>
        /// Reads the value as a string assuming it is a base 64 string and converts the string to a MemoryStream.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="typeToConvert"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public override MemoryStream Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dataBase64 = reader.GetString();
            var dataBytes = Convert.FromBase64String(dataBase64);
            var ms = new MemoryStream(dataBytes);
            return ms;
        }

        /// <summary>
        /// Writes the MemoryStream as a base 64 string.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public override void Write(Utf8JsonWriter writer, MemoryStream value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Convert.ToBase64String(value.ToArray()));
        }
    }
}
