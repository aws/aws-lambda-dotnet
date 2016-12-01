namespace Amazon.Lambda.APIGatewayEvents
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// The response object for Lambda functions handling request from from API Gateway proxy
    /// http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-set-up-simple-proxy.html
    /// </summary>
    [DataContract]
    public class APIGatewayProxyResponse
    {
        /// <summary>
        /// The HTTP status code for the request
        /// </summary>
        [DataMember(Name = "statusCode")]
        public int StatusCode { get; set; }

        /// <summary>
        /// The Http headers return in the response
        /// </summary>
        [DataMember(Name = "headers")]
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The response body
        /// </summary>
        [DataMember(Name = "body")]
        public string Body { get; set; }
    }
}
