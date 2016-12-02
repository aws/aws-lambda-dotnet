using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NewtonsoftJsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Custom JSON converter for handling special event cases.
    /// </summary>
    internal class JsonToMemoryStreamListDataConverter : JsonConverter
    {
        private static readonly TypeInfo MEMORYSTREAM_LIST_TYPEINFO = typeof(List<MemoryStream>).GetTypeInfo();

        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override bool CanConvert(Type objectType)
        {
            return MEMORYSTREAM_LIST_TYPEINFO.IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, NewtonsoftJsonSerializer serializer)
        {
            var list = new List<MemoryStream>();
            if (reader.TokenType == JsonToken.StartArray)
            {
                do
                {
                    reader.Read();
                    if (reader.TokenType == JsonToken.String)
                    {
                        var dataBase64 = reader.Value as string;
                        var ms = Common.Base64ToMemoryStream(dataBase64);
                        list.Add(ms);
                    }
                } while (reader.TokenType != JsonToken.EndArray);
            }

            return list;
        }

        public override void WriteJson(JsonWriter writer, object value, NewtonsoftJsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }

}