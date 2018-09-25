using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Exception thrown when errors occur serializing and deserializng JSON documents from the Lambda service
    /// </summary>
    public class JsonSerializerException : Exception
    {
        /// <summary>
        /// Constructs instances of JsonSerializerException
        /// </summary>
        /// <param name="message">Exception message</param>
        public JsonSerializerException(string message) : base(message) { }

        /// <summary>
        /// Constructs instances of JsonSerializerException
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="exception">Inner exception for the JsonSerializerException</param>
        public JsonSerializerException(string message, Exception exception) : base(message, exception) { }
    }
}
