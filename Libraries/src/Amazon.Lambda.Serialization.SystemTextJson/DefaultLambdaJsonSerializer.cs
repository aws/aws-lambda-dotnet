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
    public class DefaultLambdaJsonSerializer : ILambdaSerializer
    {
        private const string DEBUG_ENVIRONMENT_VARIABLE_NAME = "LAMBDA_NET_SERIALIZER_DEBUG";
        private readonly bool _debug;
        
        /// <summary>
        /// The options used to serialize JSON object.
        /// </summary>
        protected JsonSerializerOptions SerializerOptions { get; }

        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>        
        public DefaultLambdaJsonSerializer()
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
                    new ConstantClassConverter()
                }
            };

            this._debug = string.Equals(Environment.GetEnvironmentVariable(DEBUG_ENVIRONMENT_VARIABLE_NAME), "true",
                StringComparison.OrdinalIgnoreCase); 
        }

        /// <summary>
        /// Constructs instance of serializer with the option to customize the JsonSerializerOptions after the 
        /// Amazon.Lambda.Serialization.SystemTextJson's default settings have been applied.
        /// </summary>
        /// <param name="customizer"></param>
        public DefaultLambdaJsonSerializer(Action<JsonSerializerOptions> customizer)
            : this()
        {
            customizer?.Invoke(this.SerializerOptions);
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
                    using (var utf8Writer = new Utf8JsonWriter(responseStream))
                    {
                        JsonSerializer.Serialize(utf8Writer, response, SerializerOptions);

                        var jsonDocument = debugWriter.ToString();
                        Console.WriteLine($"Lambda Serialize {response.GetType().FullName}: {jsonDocument}");

                        var writer = new StreamWriter(responseStream);
                        writer.Write(jsonDocument);
                        writer.Flush();
                    }
                }
                else
                {
                    using (var writer = new Utf8JsonWriter(responseStream))
                    {
                        JsonSerializer.Serialize(writer, response, SerializerOptions);
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

                return JsonSerializer.Deserialize<T>(utf8Json, SerializerOptions);
            }
            catch (Exception e)
            {
                string message;
                var targetType = typeof(T);
                if (targetType == typeof(string))
                {
                    message =
                        $"Error converting the Lambda event JSON payload to a string. JSON strings must be quoted, for example \"Hello World\" in order to be converted to a string: {e.Message}";
                }
                else
                {
                    message =
                        $"Error converting the Lambda event JSON payload to type {targetType.FullName}: {e.Message}";
                }

                throw new JsonSerializerException(message, e);
            }
        }
    }
}