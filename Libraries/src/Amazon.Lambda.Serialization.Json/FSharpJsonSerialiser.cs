using System;
using System.IO;
using Amazon.Lambda.Core;
using Newtonsoft.Json;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Custom ILambdaSerializer implementation which uses Newtonsoft.Json 10.0.3
    /// for serialization + Fable.JsonConverter to handle F# types.
    /// 
    /// <para>
    /// If the environment variable LAMBDA_NET_SERIALIZER_DEBUG is set to true the JSON coming
    /// in from Lambda and being sent back to Lambda will be logged.
    /// </para>
    /// </summary>
    public class FSharpJsonSerializer : ILambdaSerializer
    {
        private const string DEBUG_ENVIRONMENT_VARIABLE_NAME = "LAMBDA_NET_SERIALIZER_DEBUG";
        private Newtonsoft.Json.JsonSerializer serializer;
        private bool debug;

        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>
        public FSharpJsonSerializer()
        {

            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new AwsResolver();
            serializer = Newtonsoft.Json.JsonSerializer.Create(settings);
            serializer.Converters.Add(new Fable.JsonConverter());

            if (string.Equals(Environment.GetEnvironmentVariable(DEBUG_ENVIRONMENT_VARIABLE_NAME), "true", StringComparison.OrdinalIgnoreCase))
            {
                this.debug = true;
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

        /// <summary>
        /// Deserialize a stream to a particular type.
        /// </summary>
        /// <typeparam name="T">Type of object to deserialize to.</typeparam>
        /// <param name="requestStream">Stream to serialize.</param>
        /// <returns>Deserialized object from stream.</returns>
        public T Deserialize<T>(Stream requestStream)
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
    }
}
