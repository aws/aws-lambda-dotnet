using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NewtonsoftJsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Amazon.Lambda.Serialization.Json
{
    
    internal class JsonToDictionaryStringByStringArrayDataConverter : JsonConverter
    {
        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }     
        
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, NewtonsoftJsonSerializer serializer)
        {
            var dictionary = new Dictionary<string, IList<string>>();
            if (reader.TokenType != JsonToken.StartObject)
            {
                return dictionary;
            }

            IList<string> currentValues = new List<string>();
            reader.Read();
            while (reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    var name = reader.Value.ToString();
                    currentValues = new List<string>();
                    dictionary[name] = currentValues;
                }
                else if (reader.TokenType == JsonToken.String)
                {
                    currentValues.Add(reader.Value.ToString());
                }

                reader.Read();
            }

            return dictionary;
        }

        public override void WriteJson(JsonWriter writer, object value, NewtonsoftJsonSerializer serializer)
        {
            throw new NotSupportedException();
        }        
    }
}