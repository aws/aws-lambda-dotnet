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
        [System.Text.Json.Serialization.JsonPropertyName("principalId")]
        public string PrincipalID { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="APIGatewayCustomAuthorizerPolicy"/> policy document.
        /// </summary>
        [DataMember(Name = "policyDocument")]
        [System.Text.Json.Serialization.JsonPropertyName("policyDocument")]
        public APIGatewayCustomAuthorizerPolicy PolicyDocument { get; set; } = new APIGatewayCustomAuthorizerPolicy();

        /// <summary>
        /// Gets or sets the <see cref="APIGatewayCustomAuthorizerContext"/> property.
        /// </summary>
        [DataMember(Name = "context")]
        [System.Text.Json.Serialization.JsonPropertyName("context")]
        public Dictionary<string, object> Context { get; set; }
    }
}
