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
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("principalId")]
#endif
        public string PrincipalID { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="APIGatewayCustomAuthorizerPolicy"/> policy document.
        /// </summary>
        [DataMember(Name = "policyDocument")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("policyDocument")]
#endif
        public APIGatewayCustomAuthorizerPolicy PolicyDocument { get; set; } = new APIGatewayCustomAuthorizerPolicy();

        /// <summary>
        /// Gets or sets the <see cref="APIGatewayCustomAuthorizerContext"/> property.
        /// </summary>
        [DataMember(Name = "context")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("context")]
#endif
        public APIGatewayCustomAuthorizerContextOutput Context { get; set; }

        /// <summary>
        /// Gets or sets the usageIdentifierKey.
        /// </summary>
        [DataMember(Name = "usageIdentifierKey")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("usageIdentifierKey")]
#endif
        public string UsageIdentifierKey { get; set; }
    }
}
