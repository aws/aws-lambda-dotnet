namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Options used by <see cref="IAuthorizerResult"/> to serialize into the correct API Gateway authorizer response format.
    /// These options are set by the generated handler code based on the authorizer attribute configuration.
    /// </summary>
    public class AuthorizerResultSerializationOptions
    {
        /// <summary>
        /// The authorizer response format to serialize into.
        /// </summary>
        public enum AuthorizerFormat
        {
            /// <summary>
            /// HTTP API simple response format (IsAuthorized: true/false with optional context).
            /// Used when <see cref="HttpApiAuthorizerAttribute.EnableSimpleResponses"/> is true.
            /// Produces <c>APIGatewayCustomAuthorizerV2SimpleResponse</c>.
            /// </summary>
            HttpApiSimple,

            /// <summary>
            /// HTTP API IAM policy response format.
            /// Used when <see cref="HttpApiAuthorizerAttribute.EnableSimpleResponses"/> is false.
            /// Produces <c>APIGatewayCustomAuthorizerResponse</c>.
            /// </summary>
            HttpApiIamPolicy,

            /// <summary>
            /// REST API authorizer response format (always IAM policy-based).
            /// Produces <c>APIGatewayCustomAuthorizerResponse</c>.
            /// </summary>
            RestApi
        }

        /// <summary>
        /// The authorizer response format to use for serialization.
        /// </summary>
        public AuthorizerFormat Format { get; set; }

        /// <summary>
        /// The method ARN from the authorizer request. Used to construct IAM policy documents
        /// for REST API and HTTP API IAM policy response formats.
        /// For HTTP API simple responses, this value is not used.
        /// </summary>
        public string MethodArn { get; set; }
    }
}
