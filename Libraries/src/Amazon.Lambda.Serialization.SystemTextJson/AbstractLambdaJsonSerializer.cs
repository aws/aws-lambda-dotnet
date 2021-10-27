using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Amazon.Lambda.Serialization.SystemTextJson
{
    /// <summary>
    /// Base class of serializers using System.Text.Json
    /// </summary>
    public abstract class AbstractLambdaJsonSerializer
    {
        private const string DEBUG_ENVIRONMENT_VARIABLE_NAME = "LAMBDA_NET_SERIALIZER_DEBUG";

        private readonly bool _debug;

        /// <summary>
        /// Options settings used for the JSON writer
        /// </summary>
        protected JsonWriterOptions WriterOptions { get; }

        /// <summary>
        /// Create instance
        /// </summary>
        /// <param name="jsonWriterCustomizer"></param>
        protected AbstractLambdaJsonSerializer(Action<JsonWriterOptions> jsonWriterCustomizer)
        {
            WriterOptions = new JsonWriterOptions()
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            jsonWriterCustomizer?.Invoke(this.WriterOptions);

            this._debug = string.Equals(Environment.GetEnvironmentVariable(DEBUG_ENVIRONMENT_VARIABLE_NAME), "true",
                StringComparison.OrdinalIgnoreCase);
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
                    using (var debugStream = new MemoryStream())
                    using (var utf8Writer = new Utf8JsonWriter(debugStream, WriterOptions))
                    {
                        InternalSerialize(utf8Writer, response);

                        debugStream.Position = 0;
                        using var debugReader = new StreamReader(debugStream);
                        var jsonDocument = debugReader.ReadToEnd();
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
                        InternalSerialize(writer, response);
                    }
                }
            }
            catch (Exception e)
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

                return InternalDeserialize<T>(utf8Json);
            }
            catch (Exception e)
            {
                throw new JsonSerializerException($"Error converting the Lambda event JSON payload to type {typeof(T).FullName}: {e.Message}", e);
            }
        }

        /// <summary>
        /// Perform the actual serialization after the public method had done the safety checks.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="writer"></param>
        /// <param name="response"></param>
        protected abstract void InternalSerialize<T>(Utf8JsonWriter writer, T response);

        /// <summary>
        /// Perform the actual deserialization after the public method had done the safety checks.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="utf8Json"></param>
        /// <returns></returns>
        protected abstract T InternalDeserialize<T>(byte[] utf8Json);
    }
}
