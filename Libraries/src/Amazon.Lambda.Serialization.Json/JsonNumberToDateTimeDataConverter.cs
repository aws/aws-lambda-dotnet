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
    internal class JsonNumberToDateTimeDataConverter : JsonConverter
    {
        private static readonly TypeInfo DATETIME_TYPEINFO = typeof(DateTime).GetTypeInfo();
        private static readonly DateTime EPOCH_DATETIME = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override bool CanConvert(Type objectType)
        {
            return DATETIME_TYPEINFO.IsAssignableFrom(objectType.GetTypeInfo());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, NewtonsoftJsonSerializer serializer)
        {
            double seconds;
            switch (reader.TokenType)
            {
                case JsonToken.Float:
                    seconds = (double)reader.Value;
                    break;
                case JsonToken.Integer:
                    seconds = (long)reader.Value;
                    break;
                default:
                    seconds = 0;
                    break;
            }

            var result = EPOCH_DATETIME.AddSeconds(seconds);
            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, NewtonsoftJsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }

}