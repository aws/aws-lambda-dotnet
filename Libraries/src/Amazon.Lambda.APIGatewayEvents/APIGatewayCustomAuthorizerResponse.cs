namespace Amazon.Lambda.APIGatewayEvents
{
    using System.Runtime.Serialization;

    /// <summary>
    /// An object representing the expected format of an API Gateway authorization response.
    /// </summary>
    [DataContract]
    public class APIGatewayCustomAuthorizerResponse
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
        public APIGatewayCustomAuthorizerContextOutput Context { get; set; }

        /// <summary>
        /// Gets or sets the usageIdentifierKey.
        /// </summary>
        [DataMember(Name = "usageIdentifierKey")]
        [System.Text.Json.Serialization.JsonPropertyName("usageIdentifierKey")]
        public string UsageIdentifierKey { get; set; }
    }
}
