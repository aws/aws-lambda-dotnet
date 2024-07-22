using System;
using System.IO;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Custom ILambdaSerializer implementation which uses Newtonsoft.Json 9.0.1
    /// for serialization.
    /// 
    /// <para>
    /// If the environment variable LAMBDA_NET_SERIALIZER_DEBUG is set to true the JSON coming
    /// in from Lambda and being sent back to Lambda will be logged.
    /// </para>
    /// </summary>
    public class JsonSerializer : ILambdaSerializer
    {
        private const string DEBUG_ENVIRONMENT_VARIABLE_NAME = "LAMBDA_NET_SERIALIZER_DEBUG";
        private Newtonsoft.Json.JsonSerializer serializer;
        private bool debug;

        /// <summary>
        /// Constructs instance of serializer. 
        /// </summary>
        /// <param name="customizeSerializerSettings">A callback to customize the serializer settings.</param>
        public JsonSerializer(Action<JsonSerializerSettings> customizeSerializerSettings)
            : this(customizeSerializerSettings, null)
        {

        }

        /// <summary>
        /// Constructs instance of serializer. This constructor is usefull to 
        /// customize the serializer settings.
        /// </summary>
        /// <param name="customizeSerializerSettings">A callback to customize the serializer settings.</param>
        /// <param name="namingStrategy">The naming strategy to use. This parameter makes it possible to change the naming strategy to camel case for example. When not provided, it uses the default Newtonsoft.Json DefaultNamingStrategy.</param>
        public JsonSerializer(Action<JsonSerializerSettings> customizeSerializerSettings, NamingStrategy namingStrategy)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            customizeSerializerSettings(settings);

            
            // Set the contract resolver *after* the custom callback has been 
            // invoked. This makes sure that we always use the good resolver.
            var resolver = new AwsResolver();
            if (namingStrategy != null)
            {
                resolver.NamingStrategy = namingStrategy;
            };
            settings.ContractResolver = resolver;
            settings.NullValueHandling = NullValueHandling.Ignore;

            serializer = Newtonsoft.Json.JsonSerializer.Create(settings);

            if (string.Equals(Environment.GetEnvironmentVariable(DEBUG_ENVIRONMENT_VARIABLE_NAME), "true", StringComparison.OrdinalIgnoreCase))
            {
                this.debug = true;
            }
        }

        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>
        public JsonSerializer()
            :this(customizeSerializerSettings: _ => { /* Nothing to customize by default. */ })
        {
        }

        /// <summary>
        /// Constructs instance of serializer using custom converters.
        /// </summary>
        public JsonSerializer(IEnumerable<JsonConverter> converters)
                :this()
        {
            if(converters != null)
            {
                foreach (var c in converters)
                {
                    serializer.Converters.Add(c);
                }
            }
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
                if (debug)
                {
                    using (StringWriter debugWriter = new StringWriter())
                    {
                        serializer.Serialize(debugWriter, response);
                        Console.WriteLine($"Lambda Serialize {response.GetType().FullName}: {debugWriter.ToString()}");

                        StreamWriter writer = new StreamWriter(responseStream);
                        writer.Write(debugWriter.ToString());
                        writer.Flush();
                    }
                }
                else
                {
                    StreamWriter writer = new StreamWriter(responseStream);
                    serializer.Serialize(writer, response);
                    writer.Flush();
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
                TextReader reader;
                if (debug)
                {
                    var json = new StreamReader(requestStream).ReadToEnd();
                    Console.WriteLine($"Lambda Deserialize {typeof(T).FullName}: {json}");
                    reader = new StringReader(json);
                }
                else
                {
                    reader = new StreamReader(requestStream);
                }

                JsonReader jsonReader = new JsonTextReader(reader);
                return serializer.Deserialize<T>(jsonReader);
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
