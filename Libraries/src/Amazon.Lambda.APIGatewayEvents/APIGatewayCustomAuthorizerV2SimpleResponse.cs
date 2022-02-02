namespace Amazon.Lambda.APIGatewayEvents
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// An object representing the expected format of an API Gateway authorization response.
    /// https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-lambda-authorizer.html
    /// </summary>
    [DataContract]
    public class APIGatewayCustomAuthorizerV2SimpleResponse
    {
        /// <summary>
        /// Gets or sets authorization result.
        /// </summary>
        [DataMember(Name = "isAuthorized")]
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("isAuthorized")]
#endif
        public bool IsAuthorized { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="APIGatewayCustomAuthorizerContext"/> property.
        /// </summary>
        [DataMember(Name = "context")]
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("context")]
#endif
        public Dictionary<string, object> Context { get; set; }
    }
}
