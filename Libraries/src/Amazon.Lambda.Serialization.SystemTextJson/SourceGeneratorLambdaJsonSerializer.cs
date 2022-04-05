#if NET6_0_OR_GREATER
using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson.Converters;

namespace Amazon.Lambda.Serialization.SystemTextJson
{
    /// <summary>
    /// ILambdaSerializer implementation that supports the source generator support of System.Text.Json. To use this serializer define
    /// a partial JsonSerializerContext class with attributes for the types to be serialized.
    /// 
    /// [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
    /// [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
    /// public partial class APIGatewaySerializerContext : JsonSerializerContext
    /// {
    /// }
    /// 
    /// Register the serializer with the LambdaSerializer attribute specifying the defined JsonSerializerContext
    /// 
    /// [assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer&ly;APIGatewayExampleImage.MyJsonContext&gt;))]
    /// 
    /// When the class is compiled it will generate all of the JSON serialization code to convert between JSON and the list types. This
    /// will avoid any reflection based serialization.
    /// </summary>
    /// <typeparam name="TSGContext"></typeparam>
    public class SourceGeneratorLambdaJsonSerializer<TSGContext> : AbstractLambdaJsonSerializer, ILambdaSerializer where TSGContext : JsonSerializerContext
    {
        TSGContext _jsonSerializerContext;

        /// <summary>
        /// The options used to serialize JSON object.
        /// </summary>
        protected JsonSerializerOptions SerializerOptions { get; }

        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>
        public SourceGeneratorLambdaJsonSerializer()
            : this(null, null)
        {

        }

        /// <summary>
        /// Constructs instance of serializer with the option to customize the JsonSerializerOptions after the 
        /// Amazon.Lambda.Serialization.SystemTextJson's default settings have been applied.
        /// </summary>
        /// <param name="customizer"></param>
        public SourceGeneratorLambdaJsonSerializer(Action<JsonSerializerOptions> customizer)
            : this(customizer, null)
        {

        }

        /// <summary>
        /// Constructs instance of serializer with the option to customize the JsonWriterOptions after the 
        /// Amazon.Lambda.Serialization.SystemTextJson's default settings have been applied.
        /// </summary>
        /// <param name="jsonWriterCustomizer"></param>
        public SourceGeneratorLambdaJsonSerializer(Action<JsonWriterOptions> jsonWriterCustomizer)
            : this(null, jsonWriterCustomizer)
        {
        }

        /// <summary>
        /// Constructs instance of serializer with the option to customize the JsonWriterOptions after the 
        /// Amazon.Lambda.Serialization.SystemTextJson's default settings have been applied.
        /// </summary>
        /// <param name="customizer"></param>
        /// <param name="jsonWriterCustomizer"></param>
        public SourceGeneratorLambdaJsonSerializer(Action<JsonSerializerOptions> customizer, Action<JsonWriterOptions> jsonWriterCustomizer)
            : base(jsonWriterCustomizer)
        {
            SerializerOptions = CreateDefaultJsonSerializationOptions();
            customizer?.Invoke(this.SerializerOptions);

            var constructor = typeof(TSGContext).GetConstructor(new Type[] { typeof(JsonSerializerOptions) });
            if(constructor == null)
            {
                throw new ApplicationException($"The serializer {typeof(TSGContext).FullName} is missing a constructor that takes in JsonSerializerOptions object");
            }

            _jsonSerializerContext = constructor.Invoke(new object[] { this.SerializerOptions }) as TSGContext;
        }


        /// <inheritdoc/>
        protected override void InternalSerialize<T>(Utf8JsonWriter writer, T response)
        {
            var jsonTypeInfo = _jsonSerializerContext.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>;
            if (jsonTypeInfo == null)
            {
                throw new JsonSerializerException($"No JsonTypeInfo registered in {_jsonSerializerContext.GetType().FullName} for type {typeof(T).FullName}.");
            }

            JsonSerializer.Serialize(writer, response, jsonTypeInfo);
        }

        /// <inheritdoc/>
        protected override T InternalDeserialize<T>(byte[] utf8Json)
        {
            var jsonTypeInfo = _jsonSerializerContext.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>;
            if (jsonTypeInfo == null)
            {
                throw new JsonSerializerException($"No JsonTypeInfo registered in {_jsonSerializerContext.GetType().FullName} for type {typeof(T).FullName}.");
            }

            return JsonSerializer.Deserialize<T>(utf8Json, jsonTypeInfo);
        }
    }
}
#endif
