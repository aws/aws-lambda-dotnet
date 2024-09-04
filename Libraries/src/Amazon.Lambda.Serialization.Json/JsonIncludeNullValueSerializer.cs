using System;
using System.IO;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Custom ILambdaSerializer implementation which uses Newtonsoft.Json 
    /// for serialization that includes null values.
    /// 
    /// <para>
    /// If the environment variable LAMBDA_NET_SERIALIZER_DEBUG is set to true the JSON coming
    /// in from Lambda and being sent back to Lambda will be logged.
    /// </para>
    /// </summary>
    public class JsonIncludeNullValueSerializer : JsonSerializer
    {
        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>
        public JsonIncludeNullValueSerializer()
        : base(CreateCustomizer())
        { }

        private static Action<JsonSerializerSettings> CreateCustomizer()
        {
            return (JsonSerializerSettings customSerializerSettings) =>
            {
                customSerializerSettings.NullValueHandling = NullValueHandling.Include;
            };
        }
    }
}
