using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Amazon.Lambda.Serialization.SystemTextJson.Converters
{
    public class MemoryStreamConverter : JsonConverter<MemoryStream>
    {
        public override MemoryStream Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dataBase64 = reader.GetString();
            var dataBytes = Convert.FromBase64String(dataBase64);
            var ms = new MemoryStream(dataBytes);
            return ms;
        }

        public override void Write(Utf8JsonWriter writer, MemoryStream value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
