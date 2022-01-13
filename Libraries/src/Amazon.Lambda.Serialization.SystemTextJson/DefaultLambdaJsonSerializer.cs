using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson.Converters;

namespace Amazon.Lambda.Serialization.SystemTextJson
{
    /// <summary>
    /// Custom ILambdaSerializer implementation which uses System.Text.Json
    /// for serialization.
    /// 
    /// <para>
    /// If the environment variable LAMBDA_NET_SERIALIZER_DEBUG is set to true the JSON coming
    /// in from Lambda and being sent back to Lambda will be logged.
    /// </para>
    /// </summary>    
    public class DefaultLambdaJsonSerializer : AbstractLambdaJsonSerializer, ILambdaSerializer
    {
        /// <summary>
        /// The options used to serialize JSON object.
        /// </summary>
        protected JsonSerializerOptions SerializerOptions { get; }

        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>        
        public DefaultLambdaJsonSerializer()
            : this(null, null)
        {

        }

        /// <summary>
        /// Constructs instance of serializer with the option to customize the JsonSerializerOptions after the 
        /// Amazon.Lambda.Serialization.SystemTextJson's default settings have been applied.
        /// </summary>
        /// <param name="customizer"></param>
        public DefaultLambdaJsonSerializer(Action<JsonSerializerOptions> customizer)
            : this(customizer, null)
        {
            
        }

        /// <summary>
        /// Constructs instance of serializer with the option to customize the JsonSerializerOptions after the 
        /// Amazon.Lambda.Serialization.SystemTextJson's default settings have been applied.
        /// </summary>
        /// <param name="customizer"></param>
        /// <param name="jsonWriterCustomizer"></param>
        public DefaultLambdaJsonSerializer(Action<JsonSerializerOptions> customizer, Action<JsonWriterOptions> jsonWriterCustomizer)
            : base(jsonWriterCustomizer)
        {
            SerializerOptions = new JsonSerializerOptions()
            {
                IgnoreNullValues = true,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = new AwsNamingPolicy(),
                Converters =
                {
                    new DateTimeConverter(),
                    new MemoryStreamConverter(),
                    new ConstantClassConverter(),
                    new ByteArrayConverter()
                }
            };

            customizer?.Invoke(this.SerializerOptions);
            jsonWriterCustomizer?.Invoke(this.WriterOptions);
        }

        /// <inheritdoc/>
        protected override void InternalSerialize<T>(Utf8JsonWriter writer, T response)
        {
            JsonSerializer.Serialize(writer, response, SerializerOptions);
        }

        /// <inheritdoc/>
        protected override T InternalDeserialize<T>(byte[] utf8Json)
        {
            return JsonSerializer.Deserialize<T>(utf8Json, SerializerOptions);
        }
    }
}