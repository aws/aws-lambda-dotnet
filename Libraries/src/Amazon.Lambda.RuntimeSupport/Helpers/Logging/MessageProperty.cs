#if NET6_0_OR_GREATER
using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    /// <summary>
    /// Represents a message property in a message template. For example the message 
    /// template "User bought {count} of {product}" has count and product as message properties.
    /// 
    /// </summary>
    public class MessageProperty
    {
        private static readonly char[] PARAM_FORMAT_DELIMITERS = { ':' };

        /// <summary>
        /// Parse the string representation of the message property without the brackets 
        /// to construct the MessageProperty. 
        /// </summary>
        /// <param name="messageToken"></param>
        public MessageProperty(ReadOnlySpan<char> messageToken)
        {
            // messageToken format is:
            // <optional-directive><name>:<optional-format-argument>

            this.MessageToken = "{" + messageToken.ToString() + "}";

            this.FormatDirective = Directive.Default;

            if (messageToken[0] == '@')
            {
                this.FormatDirective = Directive.JsonSerialization;
                messageToken = messageToken.Slice(1);
            }

            var idxOfDelimeter = messageToken.IndexOfAny(PARAM_FORMAT_DELIMITERS);
            if (idxOfDelimeter < 0)
            {
                this.Name = messageToken.ToString().Trim();
                this.FormatArgument = null;
            }
            else
            {
                this.Name = messageToken.Slice(0, idxOfDelimeter).ToString().Trim();
                this.FormatArgument = messageToken.Slice(idxOfDelimeter + 1).ToString().Trim();
                if(this.FormatArgument == string.Empty)
                {
                    this.FormatArgument = null;
                }
            }
        }

        /// <summary>
        /// The original text of the message property.
        /// </summary>
        public string MessageToken { get; private set; }

        /// <summary>
        /// Enum for controlling the formatting of complex logging arguments.
        /// </summary>
        public enum Directive {
            /// <summary>
            /// Perform a string formatting for the logging argument.
            /// </summary>
            Default, 
            /// <summary>
            /// Perform a JSON serialization on the logging argument.
            /// </summary>
            JsonSerialization 
        };

        /// <summary>
        /// The Name of the message property.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Optional format argument. If used the value will be formatted using string.Format passing in this format argument.
        /// </summary>
        public string FormatArgument { get; private set; }

        /// <summary>
        /// Optional format directive. Gives users the ability
        /// to indicate when types should be serialized to JSON when using structured logging.
        /// </summary>
        public Directive FormatDirective { get; private set; }

        /// <summary>
        /// Formats the value as a string that can be used to replace the message property token inside a message template.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public string FormatForMessage(object value)
        {
            if(value is byte[] bytes)
            {
                return FormatByteArray(bytes);
            }
            if (value is ReadOnlyMemory<byte> roBytes)
            {
                return FormatByteArray(roBytes.Span);
            }
            if (value is Memory<byte> meBytes)
            {
                return FormatByteArray(meBytes.Span);
            }
            if (value == null || value is IList || value is IDictionary)
            {
                return this.MessageToken;
            }
            if(!string.IsNullOrEmpty(this.FormatArgument))
            {
                return ApplyFormatArgument(value, this.FormatArgument);
            }
            if(value is DateTime dt)
            {
                return dt.ToString(AbstractLogMessageFormatter.DateFormat, CultureInfo.InvariantCulture);
            }
            if (value is DateTimeOffset dto)
            {
                return dto.ToString(AbstractLogMessageFormatter.DateFormat, CultureInfo.InvariantCulture);
            }
            if (value is DateOnly dateOnly)
            {
                return dateOnly.ToString(AbstractLogMessageFormatter.DateOnlyFormat, CultureInfo.InvariantCulture);
            }
            if (value is TimeOnly timeOnly)
            {
                return timeOnly.ToString(AbstractLogMessageFormatter.TimeOnlyFormat, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        /// <summary>
        /// If format argument is provided formats the value using string.Format otherwise returns
        /// the ToString value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="formatArgument"></param>
        /// <returns></returns>
        public static string ApplyFormatArgument(object value, string formatArgument)
        {
            if(string.IsNullOrEmpty(formatArgument))
            {
                return value.ToString();
            }

            try
            {
                var formattedValue = string.Format("{0:" + formatArgument + "}", value);
                return formattedValue;
            }
            catch(FormatException ex)
            {
                InternalLogger.GetDefaultLogger().LogError(ex, "Failed to apply logging format argument: " + formatArgument);
                return value.ToString();
            }
        }

        /// <summary>
        /// Formats byte span, including byte arrays, as a hex string. If the byte span is long the hex string
        /// will be truncated with a suffix added for the count of the byte span.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string FormatByteArray(ReadOnlySpan<byte> bytes)
        {
            // Follow Serilog's example of outputting at 16 bytes before stopping and adding a count suffix
            const int MAX_LENGTH = 16;

            var sb = new StringBuilder();
            for(int i = 0; i < bytes.Length; i++)
            {
                if(i == MAX_LENGTH)
                {
                    sb.Append("... (");
                    sb.Append(bytes.Length);
                    sb.Append(" bytes)");
                    break;
                }

                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
#endif