using System.Collections.Generic;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Represents the result of a Lambda authorizer function. Similar to how <see cref="IHttpResult"/> abstracts
    /// API Gateway proxy responses, this interface abstracts the authorizer response format.
    /// 
    /// The source generator will serialize this into the correct API Gateway response type based on the
    /// authorizer attribute configuration (HttpApiAuthorizer with simple/IAM responses, or RestApiAuthorizer).
    /// </summary>
    /// <remarks>
    /// Users should use the <see cref="AuthorizerResults"/> factory class to create instances of this interface.
    /// </remarks>
    /// <example>
    /// <code>
    /// [LambdaFunction]
    /// [HttpApiAuthorizer(EnableSimpleResponses = true)]
    /// public IAuthorizerResult Authorize([FromHeader("Authorization")] string auth, ILambdaContext context)
    /// {
    ///     if (IsValid(auth))
    ///         return AuthorizerResults.Allow()
    ///             .WithContext("userId", "user-123");
    ///     return AuthorizerResults.Deny();
    /// }
    /// </code>
    /// </example>
    public interface IAuthorizerResult
    {
        /// <summary>
        /// Whether the request is authorized.
        /// </summary>
        bool IsAuthorized { get; }

        /// <summary>
        /// The principal ID for the caller. Used primarily by REST API authorizers.
        /// For HTTP API simple authorizers, this value is ignored.
        /// </summary>
        string PrincipalId { get; }

        /// <summary>
        /// Context key-value pairs that are passed to downstream Lambda functions.
        /// These values can be accessed using <see cref="FromCustomAuthorizerAttribute"/> in protected endpoints.
        /// </summary>
        IDictionary<string, object> Context { get; }

        /// <summary>
        /// Add a context key-value pair that will be passed to downstream Lambda functions.
        /// </summary>
        /// <param name="key">The context key name</param>
        /// <param name="value">The context value</param>
        /// <returns>The same instance to allow fluent call pattern.</returns>
        IAuthorizerResult WithContext(string key, object value);

        /// <summary>
        /// Set the principal ID for the caller. Used by REST API and HTTP API IAM policy authorizers.
        /// </summary>
        /// <param name="principalId">The principal identifier</param>
        /// <returns>The same instance to allow fluent call pattern.</returns>
        IAuthorizerResult WithPrincipalId(string principalId);

        /// <summary>
        /// Serialize the authorizer result into the correct API Gateway response format.
        /// This is called by the generated Lambda handler code.
        /// </summary>
        /// <param name="options">Serialization options that determine the output format</param>
        /// <returns>The serialized response object (type depends on the authorizer format)</returns>
        object Serialize(AuthorizerResultSerializationOptions options);
    }
}
