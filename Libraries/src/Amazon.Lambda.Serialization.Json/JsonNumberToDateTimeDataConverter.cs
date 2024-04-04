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
        // The number of seconds from DateTime.MinValue to year 5000.
        private const long YEAR_5000_IN_SECONDS = 157753180800;
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

            object result;

            // If the time is in seconds is greater then the year 5000 it is safe to assume
            // this is the special case of Kinesis sending the data which actually sends the time in milliseconds.
            // https://github.com/aws/aws-lambda-dotnet/issues/839
            if (seconds > YEAR_5000_IN_SECONDS)
            {
                result = EPOCH_DATETIME.AddMilliseconds(seconds);
            }
            else
            {
                result = EPOCH_DATETIME.AddSeconds(seconds);
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, NewtonsoftJsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }

}