using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Marks this Lambda function as an HTTP API (API Gateway V2) authorizer.
    /// Other functions can reference this authorizer using the HttpApi attribute's Authorizer property.
    /// </summary>
    /// <remarks>
    /// This attribute must be used in conjunction with the <see cref="LambdaFunctionAttribute"/>.
    /// The authorizer function should return <see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayCustomAuthorizerV2SimpleResponse"/>
    /// when <see cref="EnableSimpleResponses"/> is true, or <see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayCustomAuthorizerV2IamResponse"/>
    /// when <see cref="EnableSimpleResponses"/> is false.
    /// </remarks>
    /// <example>
    /// <code>
    /// [LambdaFunction]
    /// [HttpApiAuthorizer(Name = "MyAuthorizer")]
    /// public APIGatewayCustomAuthorizerV2SimpleResponse Authorize(APIGatewayCustomAuthorizerV2Request request)
    /// {
    ///     // Validate token and return authorization response
    /// }
    /// 
    /// [LambdaFunction]
    /// [HttpApi(LambdaHttpMethod.Get, "/api/protected", Authorizer = "MyAuthorizer")]
    /// public string ProtectedEndpoint()
    /// {
    ///     return "Hello, authenticated user!";
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpApiAuthorizerAttribute : Attribute
    {
        /// <summary>
        /// Required. Unique name to identify this authorizer. Other functions reference this name
        /// via the <see cref="HttpApiAttribute.Authorizer"/> property.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Header name to use as identity source. Defaults to "Authorization".
        /// The generator translates this to "$request.header.{IdentityHeader}" for CloudFormation.
        /// </summary>
        public string IdentityHeader { get; set; } = "Authorization";

        /// <summary>
        /// Whether to use simple responses (IsAuthorized: true/false) or IAM policy responses.
        /// Defaults to true for simpler implementation.
        /// </summary>
        /// <remarks>
        /// When true, the authorizer should return <see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayCustomAuthorizerV2SimpleResponse"/>.
        /// When false, the authorizer should return <see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayCustomAuthorizerV2IamResponse"/>.
        /// </remarks>
        public bool EnableSimpleResponses { get; set; } = true;

        /// <summary>
        /// Authorizer payload format version. Valid values: "1.0" or "2.0".
        /// Defaults to "2.0".
        /// </summary>
        public string PayloadFormatVersion { get; set; } = "2.0";

        /// <summary>
        /// TTL in seconds for caching authorizer results. 0 = no caching. Max = 3600.
        /// Defaults to 0 (no caching).
        /// </summary>
        public int ResultTtlInSeconds { get; set; } = 0;
    }
}