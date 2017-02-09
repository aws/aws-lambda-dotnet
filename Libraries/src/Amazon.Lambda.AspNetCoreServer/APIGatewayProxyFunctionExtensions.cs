using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer;

namespace Amazon.Lambda.TestUtilities
{
    /// <summary>
    /// Extension methods for APIGatewayProxyFunction to make it easier to write tests.
    /// <para>
    /// This extension method was mainly added to help compatibility with existing tests when the signature for the FunctionHandlerAsync method from APIGatewayProxyFunction
    /// was change use streams to support logging.
    /// </para>
    /// </summary>
    public static class APIGatewayProxyFunctionExtensions
    {

        /// <summary>
        /// An overload of FunctionHandlerAsync to allow working with the typed API Gateway event classes. Implemented as an extension
        /// method to avoid confusion of using it as the function handler for the Lambda function.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="request"></param>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        public static async Task<APIGatewayProxyResponse> FunctionHandlerAsync(this APIGatewayProxyFunction function, APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            ILambdaSerializer serializer = new Amazon.Lambda.Serialization.Json.JsonSerializer();

            var requestStream = new MemoryStream();
            serializer.Serialize<APIGatewayProxyRequest>(request, requestStream);
            requestStream.Position = 0;

            var responseStream = await function.FunctionHandlerAsync(requestStream, lambdaContext);

            var response = serializer.Deserialize<APIGatewayProxyResponse>(responseStream);
            return response;
        }
    }
}
