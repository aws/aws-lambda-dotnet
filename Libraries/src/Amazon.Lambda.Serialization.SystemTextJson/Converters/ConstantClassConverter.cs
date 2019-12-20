using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Collections.Generic;

namespace Amazon.Lambda.Serialization.SystemTextJson.Converters
{
    /// <summary>
    /// JsonConvert to handle the AWS SDK for .NET custom enum classes that derive from the class called ConstantClass.
    /// </summary>
    /// <remarks>
    /// Because this package can not take a dependency on AWSSDK.Core we need to use name heuristics and reflection to determine if the type 
    /// extends from ConstantClass.
    /// </remarks>
    public class ConstantClassConverter : JsonConverter<object>
    {
        private const string CONSTANT_CLASS_NAME = "Amazon.Runtime.ConstantClass";

        private readonly static HashSet<string> ConstantClassNames = new HashSet<string>
        {
            "Amazon.S3.EventType",
            "Amazon.DynamoDBv2.OperationType",
            "Amazon.DynamoDBv2.StreamViewType"
        };

        /// <summary>
        /// Check to see if the type is derived from ConstantClass.
        /// </summary>
        /// <param name="typeToConvert"></param>
        /// <returns></returns>
        public override bool CanConvert(Type typeToConvert)
        {
            return ConstantClassNames.Contains(typeToConvert.FullName);
        }

        /// <summary>
        /// Called when a JSON document is being reading and a property is being converted to a ConstantClass type.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="typeToConvert"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return Activator.CreateInstance(typeToConvert, new object[] {value});
        }

        /// <summary>
        /// Called when writing the ConstantClass out to the JSON document.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
