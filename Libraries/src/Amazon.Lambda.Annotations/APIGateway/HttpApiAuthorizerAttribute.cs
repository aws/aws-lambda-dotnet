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
    /// // The authorizer name is automatically derived from the method name ("Authorize").
    /// [LambdaFunction]
    /// [HttpApiAuthorizer]
    /// public APIGatewayCustomAuthorizerV2SimpleResponse Authorize(APIGatewayCustomAuthorizerV2Request request)
    /// {
    ///     // Validate token and return authorization response
    /// }
    /// 
    /// // Reference the authorizer using nameof for compile-time safety
    /// [LambdaFunction]
    /// [HttpApi(LambdaHttpMethod.Get, "/api/protected", Authorizer = nameof(Authorize))]
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
        /// Authorizer payload format version. Defaults to <see cref="APIGateway.AuthorizerPayloadFormatVersion.V2"/>.
        /// Maps to the <c>AuthorizerPayloadFormatVersion</c> property in the SAM template.
        /// </summary>
        public AuthorizerPayloadFormatVersion AuthorizerPayloadFormatVersion { get; set; } = AuthorizerPayloadFormatVersion.V2;

        /// <summary>
        /// TTL in seconds for caching authorizer results. 0 = no caching. Max = 3600.
        /// Defaults to 0 (no caching).
        /// </summary>
        public int ResultTtlInSeconds { get; set; } = 0;
    }
}