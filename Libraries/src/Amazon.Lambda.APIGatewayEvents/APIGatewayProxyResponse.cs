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
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("statusCode")]
#endif
        public int StatusCode { get; set; }

        /// <summary>
        /// The Http headers return in the response. This collection supports setting single value for the same headers.
        /// If both the Headers and MultiValueHeaders collections are set API Gateway will merge the collection
        /// before returning back the headers to the caller.
        /// </summary>
        [DataMember(Name = "headers")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("headers")]
#endif
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The Http headers return in the response. This collection supports setting multiple values for the same headers.
        /// If both the Headers and MultiValueHeaders collections are set API Gateway will merge the collection
        /// before returning back the headers to the caller.
        /// </summary>
        [DataMember(Name = "multiValueHeaders")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("multiValueHeaders")]
#endif
        public IDictionary<string, IList<string>> MultiValueHeaders { get; set; }

        /// <summary>
        /// The response body
        /// </summary>
        [DataMember(Name = "body")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("body")]
#endif
        public string Body { get; set; }

        /// <summary>
        /// Flag indicating whether the body should be treated as a base64-encoded string
        /// </summary>
        [DataMember(Name = "isBase64Encoded")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("isBase64Encoded")]
#endif
        public bool IsBase64Encoded { get; set; }
    }
}
