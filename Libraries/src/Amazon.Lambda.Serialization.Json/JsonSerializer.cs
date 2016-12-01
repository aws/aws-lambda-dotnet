using Amazon.Lambda.Core;
using Newtonsoft.Json;
using System.IO;

namespace Amazon.Lambda.Serialization.Json
{
    /// <summary>
    /// Custom ILambdaSerializer implementation which uses Newtonsoft.Json 9.0.1
    /// for serialization.
    /// </summary>
    public class JsonSerializer : ILambdaSerializer
    {
        private Newtonsoft.Json.JsonSerializer serializer;

        /// <summary>
        /// Constructs instance of serializer.
        /// </summary>
        public JsonSerializer()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new AwsResolver();
            serializer = Newtonsoft.Json.JsonSerializer.Create(settings);
        }

        /// <summary>
        /// Serializes a particular object to a stream.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="response">Object to serialize.</param>
        /// <param name="responseStream">Output stream.</param>
        public void Serialize<T>(T response, Stream responseStream)
        {
            StreamWriter writer = new StreamWriter(responseStream);
            serializer.Serialize(writer, response);
            writer.Flush();
        }

        /// <summary>
        /// Deserializes a stream to a particular type.
        /// </summary>
        /// <typeparam name="T">Type of object to deserialize to.</typeparam>
        /// <param name="requestStream">Stream to serialize.</param>
        /// <returns>Deserialized object from stream.</returns>
        public T Deserialize<T>(Stream requestStream)
        {
            StreamReader reader = new StreamReader(requestStream);
            JsonReader jsonReader = new JsonTextReader(reader);
            return serializer.Deserialize<T>(jsonReader);
        }
    }
}
