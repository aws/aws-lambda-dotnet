namespace Amazon.Lambda.APIGatewayEvents
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// The response object for Lambda functions handling request from API Gateway proxy
    /// http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-set-up-simple-proxy.html
    /// </summary>
    [DataContract]
    public class APIGatewayProxyResponse
    {
        /// <summary>
        /// The HTTP status code for the request
        /// </summary>
        [DataMember(Name = "statusCode")]
        [System.Text.Json.Serialization.JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        /// <summary>
        /// The Http headers return in the response. This collection supports setting single value for the same headers.
        /// If both the Headers and MultiValueHeaders collections are set API Gateway will merge the collection
        /// before returning back the headers to the caller.
        /// </summary>
        [DataMember(Name = "headers")]
        [System.Text.Json.Serialization.JsonPropertyName("headers")]
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The Http headers return in the response. This collection supports setting multiple values for the same headers.
        /// If both the Headers and MultiValueHeaders collections are set API Gateway will merge the collection
        /// before returning back the headers to the caller.
        /// </summary>
        [DataMember(Name = "multiValueHeaders")]
        [System.Text.Json.Serialization.JsonPropertyName("multiValueHeaders")]
        public IDictionary<string, IList<string>> MultiValueHeaders { get; set; }

        /// <summary>
        /// The response body
        /// </summary>
        [DataMember(Name = "body")]
        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string Body { get; set; }

        /// <summary>
        /// Flag indicating whether the body should be treated as a base64-encoded string
        /// </summary>
        [DataMember(Name = "isBase64Encoded")]
        [System.Text.Json.Serialization.JsonPropertyName("isBase64Encoded")]
        public bool IsBase64Encoded { get; set; }
    }
}
