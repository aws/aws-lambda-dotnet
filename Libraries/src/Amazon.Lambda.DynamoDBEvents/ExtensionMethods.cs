using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using static Amazon.Lambda.DynamoDBEvents.DynamoDBEvent;

namespace Amazon.Lambda.DynamoDBEvents
{
    /// <summary>
    /// Extension methods for working with <see cref="DynamoDBEvent"/>
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Converts a dictionary representing a DynamoDB item to a JSON string.
        /// This may be useful when casting a DynamoDB Lambda event to the AWS SDK's 
        /// higher-level document and object persistence classes.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="item">Dictionary representing a DynamoDB item</param>
        /// <returns>Unformatted JSON string representing the DynamoDB item</returns>
        public static string ToJson(this Dictionary<string, AttributeValue> item)
        {
            return ToJson(item, false);
        }

        /// <summary>
        /// Converts a dictionary representing a DynamoDB item to a JSON string.
        /// This may be useful when casting a DynamoDB Lambda event to the AWS SDK's 
        /// higher-level document and object persistence classes.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="item">Dictionary representing a DynamoDB item</param>
        /// <returns>Formatted JSON string representing the DynamoDB item</returns>
        public static string ToJsonPretty(this Dictionary<string, AttributeValue> item)
        {
            return ToJson(item, true);
        }

        /// <summary>
        /// Internal entry point for converting a dictionary representing a DynamoDB item to a JSON string.
        /// </summary>
        /// <param name="item">Dictionary representing a DynamoDB item</param>
        /// <param name="prettyPrint">Whether the resulting JSON should be formatted</param>
        /// <returns>JSON string representing the DynamoDB item</returns>
        private static string ToJson(Dictionary<string, AttributeValue> item, bool prettyPrint)
        {
            if (item == null || item.Count == 0)
            {
                return "{}";
            }

            var stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = prettyPrint});

            WriteJson(writer, item);

            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Writes a single DynamoDB attribute as a json. May be called recursively for maps.
        /// </summary>
        /// <param name="writer">JSON writer</param>
        /// <param name="item">Dictionary representing a DynamoDB item, or a map within an item</param>
        private static void WriteJson(Utf8JsonWriter writer, Dictionary<string, AttributeValue> item)
        {
            writer.WriteStartObject();

            foreach (var attribute in item)
            {
                writer.WritePropertyName(attribute.Key);
                WriteJsonValue(writer, attribute.Value);
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes a single DynamoDB attribute value as a json value
        /// </summary>
        /// <param name="writer">JSON writer</param>
        /// <param name="attribute">DynamoDB attribute</param>
        private static void WriteJsonValue(Utf8JsonWriter writer, AttributeValue attribute)
        {
            if (attribute.S != null)
            {
                writer.WriteStringValue(attribute.S);
            }
            else if (attribute.N != null)
            {
#if NETCOREAPP3_1  // WriteRawValue was added in .NET 6, but we need to write out Number values without quotes
                using (var document = JsonDocument.Parse(attribute.N))
                {
                    document.WriteTo(writer);
                }
#else
                writer.WriteRawValue(attribute.N);
#endif
            }
            else if (attribute.B != null)
            {
                writer.WriteBase64StringValue(attribute.B.ToArray());
            }
            else if (attribute.BOOL != null)
            {
                writer.WriteBooleanValue(attribute.BOOL.Value);
            }
            else if (attribute.NULL != null)
            {
                writer.WriteNullValue();
            }
            else if (attribute.M != null)
            {
                WriteJson(writer, attribute.M);
            }
            else if (attribute.L != null)
            {
                writer.WriteStartArray();
                foreach (var item in attribute.L)
                {
                    WriteJsonValue(writer, item);
                }
                writer.WriteEndArray();
            }
            else if (attribute.SS != null)
            {
                writer.WriteStartArray();
                foreach (var item in attribute.SS)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
            }
            else if (attribute.NS != null)
            {
                writer.WriteStartArray();
                foreach (var item in attribute.NS)
                {
#if NETCOREAPP3_1  // WriteRawValue was added in .NET 6, but we need to write out Number values without quotes
                    using (var document = JsonDocument.Parse(item))
                    {
                        document.WriteTo(writer);
                    }
#else
                    writer.WriteRawValue(item);
#endif
                }
                writer.WriteEndArray();
            }
            else if (attribute.BS != null)
            {
                writer.WriteStartArray();
                foreach (var item in attribute.BS)
                {
                    writer.WriteBase64StringValue(item.ToArray());
                }
                writer.WriteEndArray();
            }
        }
    }
}
