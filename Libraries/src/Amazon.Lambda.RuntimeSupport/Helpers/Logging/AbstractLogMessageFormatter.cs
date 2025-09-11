#if NET6_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    /// <summary>
    /// Base class of log message formatters.
    /// </summary>
    public abstract class AbstractLogMessageFormatter : ILogMessageFormatter
    {
        /// <summary>
        /// Use a cache to look-up formatter so we don't have to parse the format for every entry.
        /// </summary>
        private static readonly ConcurrentDictionary<string, IReadOnlyList<MessageProperty>> MESSAGE_TEMPLATE_PARSE_CACHE = new ConcurrentDictionary<string, IReadOnlyList<MessageProperty>>();
        private const int MESSAGE_TEMPLATE_PARSE_CACHE_MAXSIZE = 1024;

        /// <summary>
        /// States in the log format parser state machine.
        /// </summary>
        private enum LogFormatParserState : byte
        {
            InMessage,
            PossibleParameterOpen,
            InParameter
        }

        /// <summary>
        /// Parse the message template for all message properties.
        /// </summary>
        /// <param name="messageTemplate">The message template users passed in as the log message.</param>
        /// <returns>List of MessageProperty objects detected by parsing the message template.</returns>
        public virtual IReadOnlyList<MessageProperty> ParseProperties(string messageTemplate)
        {
            // Check to see if this message template has already been parsed before.
            if (MESSAGE_TEMPLATE_PARSE_CACHE.TryGetValue(messageTemplate, out var cachedMessageProperties))
            {
                return cachedMessageProperties;
            }

            var messageProperties = new List<MessageProperty>();
            var state = LogFormatParserState.InMessage;

            int paramStartIdx = -1;
            for (int i = 0, l = messageTemplate.Length; i < l; i++)
            {
                var c = messageTemplate[i];
                switch (c)
                {
                    case '{':
                        if (state == LogFormatParserState.InMessage)
                        {
                            state = LogFormatParserState.PossibleParameterOpen;
                        }
                        else if (state == LogFormatParserState.PossibleParameterOpen)
                        {
                            // this is an escaped brace
                            state = LogFormatParserState.InMessage;
                        }
                        break;
                    case '}':
                        if (state != LogFormatParserState.InMessage)
                        {
                            if(paramStartIdx != -1)
                            {
                                // Since we have a closing bracket and there is at least a start to a message property label 
                                // then we know we have hit the end of the message property.
                                messageProperties.Add(new MessageProperty(messageTemplate.AsSpan().Slice(paramStartIdx, i - paramStartIdx)));
                            }

                            state = LogFormatParserState.InMessage;
                            paramStartIdx = -1;
                        }
                        break;
                    default:
                        if (state == LogFormatParserState.PossibleParameterOpen)
                        {
                            // non-brace character after '{', transition to InParameter
                            paramStartIdx = i;
                            state = LogFormatParserState.InParameter;
                        }
                        break;
                }
            }

            var readonlyMessagesProperties = messageProperties.AsReadOnly();

            // If there is a room in the message template cache then cache the parse results for
            // later logging performance increase.
            if (MESSAGE_TEMPLATE_PARSE_CACHE.Count < MESSAGE_TEMPLATE_PARSE_CACHE_MAXSIZE)
            {
                MESSAGE_TEMPLATE_PARSE_CACHE.TryAdd(messageTemplate, readonlyMessagesProperties);
            }

            return readonlyMessagesProperties;
        }

        /// <summary>
        /// Subclasses to implement to format the message given the requirements of the subclass.
        /// </summary>
        /// <param name="state">The state of the message to log.</param>
        /// <returns>The full log message to send to CloudWatch Logs.</returns>
        public abstract string FormatMessage(MessageState state);

        internal const string DateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        internal const string DateOnlyFormat = "yyyy-MM-dd";

        internal const string TimeOnlyFormat = "HH:mm:ss.fff";

        /// <summary>
        /// Format the timestamp of the log message in format Lambda service prefers.
        /// </summary>
        /// <param name="state">The state of the message to log.</param>
        /// <returns>Timestamp formatted for logging.</returns>
        protected string FormatTimestamp(MessageState state)
        {
            return state.TimeStamp.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Replace all message properties in message templates with formatted values from the arguments passed in.
        /// </summary>
        /// <param name="messageTemplate"></param>
        /// <param name="messageProperties"></param>
        /// <param name="messageArguments"></param>
        /// <returns>The log message with logging arguments replaced with the values.</returns>
        public string ApplyMessageProperties(string messageTemplate, IReadOnlyList<MessageProperty> messageProperties, object[] messageArguments)
        {
            if(messageProperties.Count == 0 || messageArguments == null || messageArguments.Length == 0)
            {
                return messageTemplate;
            }

            // Check to see if the message template is using positions instead of names. For example
            // "User bought {0} of {1}" is positional as opposed to "User bought {count} of {product"}.
            var usePositional = UsingPositionalArguments(messageProperties);

            var state = LogFormatParserState.InMessage;

            // Builder used to create the message with the properties replaced. Set the initial capacity
            // to the size of the message template plus 10% to give room for the values of message properties 
            // to be larger then the message property in the message themselves.
            StringBuilder messageBuilder = new StringBuilder(capacity: (int)(messageTemplate.Length * 1.1));

            // Builder used store potential message properties as we parse through the message. If the 
            // it turns out the potential message property is not a message property the contents of this
            // builder are written back to the messageBuilder.
            StringBuilder propertyBuilder = new StringBuilder();

            int propertyIndex = 0;
            for (int i = 0, l = messageTemplate.Length; i < l; i++)
            {
                var c = messageTemplate[i];

                // If not using positional properties and we have hit the point there are more message properties then arguments
                // then just add the rest of the message template onto the messageBuilder.
                if (!usePositional && (messageProperties.Count <= propertyIndex || messageArguments.Length <= propertyIndex))
                {
                    messageBuilder.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '{':
                        if (state == LogFormatParserState.InMessage)
                        {
                            // regardless of whether this is the opening of a parameter we'd still need to add {
                            propertyBuilder.Append(c);
                            state = LogFormatParserState.PossibleParameterOpen;
                        }
                        else if (state == LogFormatParserState.PossibleParameterOpen)
                        {
                            // We have hit an escaped "{" by the user using "{{". Since we now know we are
                            // not in a message properties write back the propertiesBuilder into 
                            // messageBuilder and reset the propertyBuilder.
                            messageBuilder.Append(propertyBuilder.ToString());
                            propertyBuilder.Clear();
                            messageBuilder.Append(c);
                            state = LogFormatParserState.InMessage;
                        }
                        else
                        {
                            propertyBuilder.Append(c);
                        }
                        break;
                    case '}':
                        if (state == LogFormatParserState.InMessage)
                        {
                            messageBuilder.Append(c);
                        }
                        else
                        {
                            var property = messageProperties[propertyIndex];
                            object argument = null;
                            if(usePositional)
                            {
                                // If usePositional is true then we have confirmed the `Name` property is an int
                                var index = int.Parse(property.Name, CultureInfo.InvariantCulture);
                                if (index < messageArguments.Length)
                                {
                                    argument = messageArguments[index];
                                }
                            }
                            else
                            {
                                argument = messageArguments[propertyIndex];
                            }
                            messageBuilder.Append(property.FormatForMessage(argument));

                            propertyIndex++;
                            propertyBuilder.Clear();
                            state = LogFormatParserState.InMessage;
                        }
                        break;
                    default:
                        if (state == LogFormatParserState.InMessage)
                        {
                            messageBuilder.Append(c);
                        }
                        else if (state == LogFormatParserState.PossibleParameterOpen)
                        {
                            // non-brace character after '{', transition to InParameter
                            propertyBuilder.Append(c);
                            state = LogFormatParserState.InParameter;
                        }
                        break;
                }
            }

            return messageBuilder.ToString();
        }

        /// <summary>
        /// Check to see if the properties in a message are using a position instead of names.
        /// Positional example:
        ///     Log Message: "{0} {1} {0}"
        ///     Arguments: "Arg1", "Arg2"
        ///     Formatted Message: "Arg1 Arg2 Arg1"
        /// Name example:
        ///     Log Message: "{name} {age} {home}
        ///     Arguments: "Lewis", 15, "Washington
        ///     Formatted Message: "Lewis 15 Washington"
        /// </summary>
        /// <param name="messageProperties"></param>
        /// <returns>True of the logging arguments are positional</returns>
        public bool UsingPositionalArguments(IReadOnlyList<MessageProperty> messageProperties)
        {
            var min = int.MaxValue;
            int max = int.MinValue;
            HashSet<int> positions = new HashSet<int>();
            foreach(var property in messageProperties)
            {
                // If any logging arguments use non-numeric identifier then they are not using positional arguments.
                if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var position))
                {
                    return false;
                }

                positions.Add(position);
                if(position < min)
                {
                    min = position;
                }
                if (max < position)
                {
                    max = position;
                }
            }

            // At this point the HashSet is the collection of all of the int logging arguments.
            // If there are no gaps or duplicates in the logging statement then the smallest value 
            // in the hashset should be 0 and the max value equals the count of the hashset. If
            // either of those conditions are not true then it can't be positional arguments.
            if(positions.Count != (max + 1) || min != 0) 
            {
                return false;
            }

            return true;
        }
    }
}
#endif
