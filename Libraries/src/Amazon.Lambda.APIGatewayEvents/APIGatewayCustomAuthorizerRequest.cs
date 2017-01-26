namespace Amazon.Lambda.APIGatewayEvents
{
    /// <summary>
    /// For requests coming in to a custom API Gateway authorizer function.
    /// </summary>
    public class APIGatewayCustomAuthorizerRequest
    {
        /// <summary>
        /// Gets or sets the 'type' property.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the 'authorizationToken' property.
        /// </summary>
        public string AuthorizationToken { get; set; }

        /// <summary>
        /// Gets or sets the 'methodArn' property.
        /// </summary>
        public string MethodArn { get; set; }
    }
}
