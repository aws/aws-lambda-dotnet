namespace Amazon.Lambda.Core
{
    using System.IO;

    /// <summary>
    /// Interface that must be implemented by custom serializers that
    /// may need to be called during execution.
    /// </summary>
    public interface ILambdaSerializer
    {
        /// <summary>
        /// This method is called to deserialize the request payload from Invoke API
        /// into the object that is passed to the Lambda function handler.
        /// </summary>
        /// <typeparam name="T">Type of object to deserialize to.</typeparam>
        /// <param name="requestStream">Stream to serialize.</param>
        /// <returns>Deserialized object from stream.</returns>
        T Deserialize<T>(Stream requestStream);

        /// <summary>
        /// This method is called to serialize the result returned from 
        /// a Lambda function handler into the response payload
        /// that is returned by the Invoke API.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="response">Object to serialize.</param>
        /// <param name="responseStream">Output stream.</param>
        void Serialize<T>(T response, Stream responseStream);
    }
}
