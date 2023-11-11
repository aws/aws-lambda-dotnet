using System.Text.Json;

namespace Amazon.Lambda.Serialization.SystemTextJson
{
    /// <summary>
    /// Custom ILambdaSerializer implementation which uses System.Text.Json
    /// for serialization.
    ///
    /// <para>
    /// When serializing objects to JSON camel casing will be used for JSON property names.
    /// </para>
    /// <para>
    /// If the environment variable LAMBDA_NET_SERIALIZER_DEBUG is set to true the JSON coming
    /// in from Lambda and being sent back to Lambda will be logged.
    /// </para>
    /// </summary>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("CamelCaseLambdaJsonSerializer does not support trimming. " +
            "For trimmed Lambda functions SourceGeneratorLambdaJsonSerializer passing in JsonSerializerContext should be used instead.")]
#endif
    public class CamelCaseLambdaJsonSerializer : DefaultLambdaJsonSerializer
    {
        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>
        public CamelCaseLambdaJsonSerializer()
            : base(ConfigureJsonSerializerOptions)
        {
            
        }

        private static void ConfigureJsonSerializerOptions(JsonSerializerOptions options)
        {
            options.PropertyNamingPolicy = new AwsNamingPolicy(JsonNamingPolicy.CamelCase);
        }
    }
}