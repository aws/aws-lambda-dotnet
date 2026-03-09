using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Type of REST API Lambda authorizer
    /// </summary>
    public enum RestApiAuthorizerType
    {
        /// <summary>
        /// Token-based authorizer. Receives the token directly from the identity source.
        /// The token is available via request.AuthorizationToken.
        /// </summary>
        Token,

        /// <summary>
        /// Request-based authorizer. Receives the full request context including
        /// headers, query strings, path parameters, and stage variables.
        /// </summary>
        Request
    }

    /// <summary>
    /// Marks this Lambda function as a REST API (API Gateway V1) authorizer.
    /// Other functions can reference this authorizer using the RestApi attribute's Authorizer property.
    /// </summary>
    /// <remarks>
    /// This attribute must be used in conjunction with the <see cref="LambdaFunctionAttribute"/>.
    /// The authorizer function should return <see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayCustomAuthorizerResponse"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // The authorizer name is automatically derived from the method name ("Authorize").
    /// [LambdaFunction]
    /// [RestApiAuthorizer(Type = RestApiAuthorizerType.Token)]
    /// public APIGatewayCustomAuthorizerResponse Authorize(APIGatewayCustomAuthorizerRequest request)
    /// {
    ///     var token = request.AuthorizationToken;
    ///     // Validate token and return IAM policy response
    /// }
    /// 
    /// // Reference the authorizer using nameof for compile-time safety
    /// [LambdaFunction]
    /// [RestApi(LambdaHttpMethod.Get, "/api/protected", Authorizer = nameof(Authorize))]
    /// public string ProtectedEndpoint()
    /// {
    ///     return "Hello, authenticated user!";
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method)]
    public class RestApiAuthorizerAttribute : Attribute
    {
        /// <summary>
        /// Header name to use as identity source. Defaults to "Authorization".
        /// The generator translates this to "method.request.header.{IdentityHeader}" for CloudFormation.
        /// </summary>
        public string IdentityHeader { get; set; } = "Authorization";

        /// <summary>
        /// Type of authorizer: Token or Request. Defaults to Token.
        /// Token authorizers receive just the token value; Request authorizers receive full request context.
        /// </summary>
        public RestApiAuthorizerType Type { get; set; } = RestApiAuthorizerType.Token;

        /// <summary>
        /// TTL in seconds for caching authorizer results. 0 = no caching. Max = 3600.
        /// Defaults to 0 (no caching).
        /// </summary>
        public int ResultTtlInSeconds { get; set; } = 0;
    }
}