using Amazon.Lambda.Annotations.APIGateway;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Enumeration for the type of API Gateway authorizer
    /// </summary>
    public enum AuthorizerType
    {
        /// <summary>
        /// HTTP API (API Gateway V2) authorizer
        /// </summary>
        HttpApi,

        /// <summary>
        /// REST API (API Gateway V1) authorizer
        /// </summary>
        RestApi
    }

    /// <summary>
    /// Model representing a Lambda Authorizer configuration
    /// </summary>
    public class AuthorizerModel
    {
        /// <summary>
        /// Unique name to identify this authorizer. Functions reference this name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The CloudFormation resource name for the Lambda function that implements this authorizer.
        /// This is derived from the LambdaFunctionAttribute's ResourceName or the generated method name.
        /// </summary>
        public string LambdaResourceName { get; set; }

        /// <summary>
        /// The type of API Gateway authorizer (HTTP API or REST API)
        /// </summary>
        public AuthorizerType AuthorizerType { get; set; }

        /// <summary>
        /// Header name to use as identity source.
        /// </summary>
        public string IdentityHeader { get; set; }

        /// <summary>
        /// TTL in seconds for caching authorizer results.
        /// </summary>
        public int ResultTtlInSeconds { get; set; }

        // HTTP API specific properties

        /// <summary>
        /// Whether to use simple responses (IsAuthorized: true/false) or IAM policy responses.
        /// Only applicable for HTTP API authorizers.
        /// </summary>
        public bool EnableSimpleResponses { get; set; }

        /// <summary>
        /// Authorizer payload format version.
        /// Only applicable for HTTP API authorizers.
        /// </summary>
        public AuthorizerPayloadFormatVersion AuthorizerPayloadFormatVersion { get; set; }

        // REST API specific properties

        /// <summary>
        /// Type of REST API authorizer: Token or Request.
        /// Only applicable for REST API authorizers.
        /// </summary>
        public RestApiAuthorizerType RestApiAuthorizerType { get; set; }

        /// <summary>
        /// Gets the identity source string formatted for CloudFormation.
        /// </summary>
        /// <returns>The formatted identity source string</returns>
        public string GetIdentitySource()
        {
            if (AuthorizerType == AuthorizerType.HttpApi)
            {
                return $"$request.header.{IdentityHeader}";
            }
            else
            {
                return $"method.request.header.{IdentityHeader}";
            }
        }

        /// <summary>
        /// Gets the CloudFormation resource name for this authorizer.
        /// </summary>
        /// <returns>The CloudFormation resource name</returns>
        public string GetAuthorizerResourceName()
        {
            return $"{Name}Authorizer";
        }
    }
}