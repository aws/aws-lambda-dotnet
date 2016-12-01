using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using NewtonsoftJsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Custom JSON converter for handling special event cases.
    /// </summary>
    internal class KinesisEventRecordDataConverter : JsonConverter
    {
        private static readonly TypeInfo MEMORYSTREAM_TYPEINFO = typeof(MemoryStream).GetTypeInfo();

        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override bool CanConvert(Type objectType)
        {
            return MEMORYSTREAM_TYPEINFO.IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, NewtonsoftJsonSerializer serializer)
        {
            var dataBase64 = reader.Value as string;
            var dataBytes = Convert.FromBase64String(dataBase64);
            MemoryStream stream = new MemoryStream(dataBytes);
            return stream;
        }

        public override void WriteJson(JsonWriter writer, object value, NewtonsoftJsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }

}