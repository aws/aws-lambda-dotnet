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
    /// <para>
    /// This serializer is obsolete because it uses inconsistent name casing when serializing to JSON. Fixing the
    /// inconsistent casing issues would cause runtime breaking changes so the new type DefaultLambdaJsonSerializer was created.
    /// https://github.com/aws/aws-lambda-dotnet/issues/624
    /// </para>
    /// </summary>    
    [Obsolete("This serializer is obsolete because it uses inconsistent name casing when serializing to JSON. Lambda functions should use the DefaultLambdaJsonSerializer type.")]
    public class LambdaJsonSerializer : ILambdaSerializer
    {
        private const string DEBUG_ENVIRONMENT_VARIABLE_NAME = "LAMBDA_NET_SERIALIZER_DEBUG";
        private readonly JsonSerializerOptions _options;
        private readonly JsonWriterOptions WriterOptions;
        private readonly bool _debug;

        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>        
        public LambdaJsonSerializer()
        {
            _options = new JsonSerializerOptions()
            {
                IgnoreNullValues = true,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = new AwsNamingPolicy(JsonNamingPolicy.CamelCase)
            };

            _options.Converters.Add(new DateTimeConverter());
            _options.Converters.Add(new MemoryStreamConverter());
            _options.Converters.Add(new ConstantClassConverter());
            _options.Converters.Add(new ByteArrayConverter());

            WriterOptions = new JsonWriterOptions()
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            if (string.Equals(Environment.GetEnvironmentVariable(DEBUG_ENVIRONMENT_VARIABLE_NAME), "true", StringComparison.OrdinalIgnoreCase))
            {
                this._debug = true;
            }            
        }

        /// <summary>
        /// Constructs instance of serializer with the option to customize the JsonSerializerOptions after the 
        /// Amazon.Lambda.Serialization.SystemTextJson's default settings have been applied.
        /// </summary>
        /// <param name="customizer"></param>
        public LambdaJsonSerializer(Action<JsonSerializerOptions> customizer)
            : this()
        {
            customizer?.Invoke(this._options);
        }

        /// <summary>
        /// Constructs instance of serializer with the option to customize the JsonSerializerOptions after the 
        /// Amazon.Lambda.Serialization.SystemTextJson's default settings have been applied.
        /// </summary>
        /// <param name="customizer"></param>
        /// <param name="jsonWriterCustomizer"></param>
        public LambdaJsonSerializer(Action<JsonSerializerOptions> customizer, Action<JsonWriterOptions> jsonWriterCustomizer)
            : this(customizer)
        {
            jsonWriterCustomizer?.Invoke(this.WriterOptions);
        }

        /// <summary>
        /// Serializes a particular object to a stream.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="response">Object to serialize.</param>
        /// <param name="responseStream">Output stream.</param>        
        public void Serialize<T>(T response, Stream responseStream)
        {
            try
            {
                if (_debug)
                {
                    using (var debugWriter = new StringWriter())
                    using (var utf8Writer = new Utf8JsonWriter(responseStream, WriterOptions))
                    {
                        JsonSerializer.Serialize(utf8Writer, response);

                        var jsonDocument = debugWriter.ToString();
                        Console.WriteLine($"Lambda Serialize {response.GetType().FullName}: {jsonDocument}");

                        var writer = new StreamWriter(responseStream);
                        writer.Write(jsonDocument);
                        writer.Flush();
                    }
                }
                else
                {
                    using (var writer = new Utf8JsonWriter(responseStream, WriterOptions))
                    {
                        JsonSerializer.Serialize(writer, response, _options);
                    }
                }
            }
            catch(Exception e)
            {
                throw new JsonSerializerException($"Error converting the response object of type {typeof(T).FullName} from the Lambda function to JSON: {e.Message}", e);
            }            
        }  
        
        /// <summary>
        /// Deserializes a stream to a particular type.
        /// </summary>
        /// <typeparam name="T">Type of object to deserialize to.</typeparam>
        /// <param name="requestStream">Stream to serialize.</param>
        /// <returns>Deserialized object from stream.</returns>
        public T Deserialize<T>(Stream requestStream)
        {
            try
            {
                byte[] utf8Json = null;
                if (_debug)
                {
                    var json = new StreamReader(requestStream).ReadToEnd();
                    Console.WriteLine($"Lambda Deserialize {typeof(T).FullName}: {json}");
                    utf8Json = UTF8Encoding.UTF8.GetBytes(json);
                }

                if (utf8Json == null)
                {
                    if (requestStream is MemoryStream ms)
                    {
                        utf8Json = ms.ToArray();
                    }
                    else
                    {
                        using (var copy = new MemoryStream())
                        {
                            requestStream.CopyTo(copy);
                            utf8Json = copy.ToArray();
                        }
                    }
                }

                return JsonSerializer.Deserialize<T>(utf8Json, _options);
            }
            catch (Exception e)
            {
                string message;
                var targetType = typeof(T);
                if(targetType == typeof(string))
                {
                    message = $"Error converting the Lambda event JSON payload to a string. JSON strings must be quoted, for example \"Hello World\" in order to be converted to a string: {e.Message}";
                }
                else
                {
                    message = $"Error converting the Lambda event JSON payload to type {targetType.FullName}: {e.Message}";
                }
                throw new JsonSerializerException(message, e);
            }            
        }        
    }
}