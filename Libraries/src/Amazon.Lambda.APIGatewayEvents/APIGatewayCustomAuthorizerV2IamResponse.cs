namespace Amazon.Lambda.APIGatewayEvents
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// An object representing the expected format of an API Gateway authorization response.
    /// https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-lambda-authorizer.html
    /// </summary>
    [DataContract]
    public class APIGatewayCustomAuthorizerV2IamResponse
    {
        /// <summary>
        /// Gets or sets the ID of the principal.
        /// </summary>
        [DataMember(Name = "principalId")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("principalId")]
#endif
        public string PrincipalID { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="APIGatewayCustomAuthorizerPolicy"/> policy document.
        /// </summary>
        [DataMember(Name = "policyDocument")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("policyDocument")]
#endif
        public APIGatewayCustomAuthorizerPolicy PolicyDocument { get; set; } = new APIGatewayCustomAuthorizerPolicy();

        /// <summary>
        /// Gets or sets the <see cref="APIGatewayCustomAuthorizerContext"/> property.
        /// </summary>
        [DataMember(Name = "context")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("context")]
#endif
        public Dictionary<string, object> Context { get; set; }
    }
}
