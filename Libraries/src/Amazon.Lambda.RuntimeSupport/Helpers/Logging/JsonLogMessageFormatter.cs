#if NET6_0_OR_GREATER
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    /// <summary>
    /// Formats the log message as a structured JSON log message.
    /// </summary>
    public class JsonLogMessageFormatter : AbstractLogMessageFormatter
    {
        private static readonly UTF8Encoding UTF8NoBomNoThrow = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

        // Options used when serializing any message property values as a JSON to be added to the structured log message.
        private JsonSerializerOptions _jsonSerializationOptions;

        /// <summary>
        /// Constructs an instance of JsonLogMessageFormatter.
        /// </summary>
        public JsonLogMessageFormatter()
        {
            _jsonSerializationOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
        }

        private static readonly IReadOnlyList<MessageProperty> _emptyMessageProperties = new List<MessageProperty>();

        /// <summary>
        /// Format the log message as a structured JSON log message.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public override string FormatMessage(MessageState state)
        {
            IReadOnlyList<MessageProperty> messageProperties;
            string message;
            // If there are no arguments then this is not a parameterized log message so skip parsing logic.
            if (state.MessageArguments?.Length == 0)
            {
                messageProperties = _emptyMessageProperties;
                message = state.MessageTemplate ?? string.Empty;
            }
            else
            {
                // Parse the message template for any message properties like "{count}".
                messageProperties = ParseProperties(state.MessageTemplate ?? string.Empty);

                // Replace any message properties in the message template with the provided argument values.
                message = ApplyMessageProperties(state.MessageTemplate ?? string.Empty, messageProperties, state.MessageArguments);
            }


            var bufferWriter = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions
            {
                Indented = false
            });
            writer.WriteStartObject();

            writer.WriteString("timestamp", FormatTimestamp(state));

            // Following Serilog's example and use the full name of the level instead of the
            // abbreviating done DefaultLogMessageFormatter which follows Microsoft's ILogger console format.
            // All structured logging should have a log level. If one is not given the default to Information as the log level.
            writer.WriteString("level", state.Level?.ToString() ?? LogLevelLoggerWriter.LogLevel.Information.ToString());

            if (!string.IsNullOrEmpty(state.AwsRequestId))
            {
                writer.WriteString("requestId", state.AwsRequestId);
            }
            
            if (!string.IsNullOrEmpty(state.TraceId))
            {
                writer.WriteString("traceId", state.TraceId);
            }

            writer.WriteString("message", message);

            // Add any message properties as JSON properties to the structured log.
            WriteMessageAttributes(writer, messageProperties, state);

            WriteException(writer, state);

            writer.WriteEndObject();
            writer.Flush();

            return UTF8NoBomNoThrow.GetString(bufferWriter.WrittenSpan);
        }

        /// <summary>
        /// Write any message properties for the log message as top level JSON properties.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="messageProperties"></param>
        /// <param name="state"></param>
        private void WriteMessageAttributes(Utf8JsonWriter writer, IReadOnlyList<MessageProperty> messageProperties, MessageState state)
        {
            // Check to see if the message template is using positions instead of names. For example
            // "User bought {0} of {1}" is positional as opposed to "User bought {count} of {product"}.
            var usePositional = UsingPositionalArguments(messageProperties);

            if (messageProperties == null)
            {
                return;
            }

            for (var i = 0; i < messageProperties.Count; i++)
            {
                object messageArgument;
                if(usePositional)
                {
                    // If usePositional is true then we have confirmed the `Name` property is an int.
                    var index = int.Parse(messageProperties[i].Name, CultureInfo.InvariantCulture);
                    if (index < state.MessageArguments.Length)
                    {
                        // Don't include null JSON properties
                        if (state.MessageArguments[index] == null)
                            continue;

                        messageArgument = state.MessageArguments[index];
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    // There are more message properties in the template then values for the properties. Skip 
                    // adding anymore JSON properties since there are no more values.
                    if (state.MessageArguments.Length <= i)
                        break;

                    // Don't include null JSON properties
                    if (state.MessageArguments[i] == null)
                        continue;

                    messageArgument = state.MessageArguments[i];

                }

                writer.WritePropertyName(messageProperties[i].Name);

                if (messageArgument is IList && messageArgument is not IList<byte>)
                {
                    writer.WriteStartArray();
                    foreach (var item in ((IList)messageArgument))
                    {
                        FormatJsonValue(writer, item, messageProperties[i].FormatArgument, messageProperties[i].FormatDirective);
                    }
                    writer.WriteEndArray();
                }
                else if (messageArgument is IDictionary)
                {
                    writer.WriteStartObject();
                    foreach (DictionaryEntry entry in ((IDictionary)messageArgument))
                    {
                        writer.WritePropertyName(entry.Key.ToString() ?? string.Empty);

                        FormatJsonValue(writer, entry.Value, messageProperties[i].FormatArgument, messageProperties[i].FormatDirective);
                    }
                    writer.WriteEndObject();
                }
                else
                {
                    FormatJsonValue(writer, messageArgument, messageProperties[i].FormatArgument, messageProperties[i].FormatDirective);
                }
            }
        }

        /// <summary>
        /// Add the exception information as top level JSON properties
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="state"></param>
        private void WriteException(Utf8JsonWriter writer, MessageState state)
        {
            if (state.Exception != null)
            {
                writer.WriteString("errorType", state.Exception.GetType().FullName);
                writer.WriteString("errorMessage", state.Exception.Message);
                writer.WritePropertyName("stackTrace");
                writer.WriteStartArray();
                foreach(var line in state.Exception.ToString().Split('\n'))
                {
                    writer.WriteStringValue(line.Trim());
                }
                writer.WriteEndArray();
            }
        }

        /// <summary>
        /// Format the value to be included in the structured log message.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="formatArguments"></param>
        /// <param name="directive"></param>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "If formatting an object using JSON serialization this will do its best attempt. If the object has trim errors formatting will fall back to ToString for the object.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("ReflectionAnalysis", "IL3050",
            Justification = "If formatting an object using JSON serialization this will do its best attempt. If the object has trim errors formatting will fall back to ToString for the object.")]
        private void FormatJsonValue(Utf8JsonWriter writer, object value, string formatArguments, MessageProperty.Directive directive)
        {
            if(value == null)
            {
                writer.WriteNullValue();
            }
            else if (directive == MessageProperty.Directive.JsonSerialization)
            {
                try
                {
                    writer.WriteRawValue(JsonSerializer.Serialize(value, _jsonSerializationOptions));
                }
                catch
                {
                    // If running in an AOT environment where the code is trimmed it is possible the reflection based serialization might fail due to code being trimmed.
                    // In that case fallback to writing the ToString version of the object.
                    writer.WriteStringValue(value.ToString());
                }
            }
            else if (!string.IsNullOrEmpty(formatArguments))
            {
                writer.WriteStringValue(MessageProperty.ApplyFormatArgument(value, formatArguments));
            }
            else
            {
                switch (value)
                {
                    case bool boolValue:
                        writer.WriteBooleanValue(boolValue);
                        break;
                    case byte byteValue:
                        writer.WriteNumberValue(byteValue);
                        break;
                    case sbyte sbyteValue:
                        writer.WriteNumberValue(sbyteValue);
                        break;
                    case char charValue:
                        writer.WriteStringValue(MemoryMarshal.CreateSpan(ref charValue, 1));
                        break;
                    case decimal decimalValue:
                        writer.WriteNumberValue(decimalValue);
                        break;
                    case double doubleValue:
                        writer.WriteNumberValue(doubleValue);
                        break;
                    case float floatValue:
                        writer.WriteNumberValue(floatValue);
                        break;
                    case int intValue:
                        writer.WriteNumberValue(intValue);
                        break;
                    case uint uintValue:
                        writer.WriteNumberValue(uintValue);
                        break;
                    case long longValue:
                        writer.WriteNumberValue(longValue);
                        break;
                    case ulong ulongValue:
                        writer.WriteNumberValue(ulongValue);
                        break;
                    case short shortValue:
                        writer.WriteNumberValue(shortValue);
                        break;
                    case ushort ushortValue:
                        writer.WriteNumberValue(ushortValue);
                        break;
                    case null:
                        writer.WriteNullValue();
                        break;
                    case DateTime dateTimeValue:
                        writer.WriteStringValue(dateTimeValue.ToString(DateFormat, CultureInfo.InvariantCulture));
                        break;
                    case DateTimeOffset dateTimeOffsetValue:
                        writer.WriteStringValue(dateTimeOffsetValue.ToString(DateFormat, CultureInfo.InvariantCulture));
                        break;
                    case DateOnly dateOnly:
                        writer.WriteStringValue(dateOnly.ToString(DateOnlyFormat, CultureInfo.InvariantCulture));
                        break;
                    case TimeOnly timeOnly:
                        writer.WriteStringValue(timeOnly.ToString(TimeOnlyFormat, CultureInfo.InvariantCulture));
                        break;
                    case byte[] byteArrayValue:
                        writer.WriteStringValue(MessageProperty.FormatByteArray(byteArrayValue));
                        break;
                    case ReadOnlyMemory<byte> roByteArrayValue:
                        writer.WriteStringValue(MessageProperty.FormatByteArray(roByteArrayValue.Span));
                        break;
                    case Memory<byte> meByteArrayValue:
                        writer.WriteStringValue(MessageProperty.FormatByteArray(meByteArrayValue.Span));
                        break;
                    default:
                        writer.WriteStringValue(ToInvariantString(value));
                        break;
                }
            }
        }

        private static string ToInvariantString(object obj) => Convert.ToString(obj, CultureInfo.InvariantCulture);
    }
}
#endif
