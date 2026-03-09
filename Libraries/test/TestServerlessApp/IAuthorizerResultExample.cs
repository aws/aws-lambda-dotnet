using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    /// <summary>
    /// Examples demonstrating the IAuthorizerResult pattern for Lambda authorizer functions.
    /// These use FromHeader to extract headers and IAuthorizerResult for simplified Allow/Deny responses.
    /// </summary>
    public class IAuthorizerResultExample
    {
        /// <summary>
        /// HTTP API V2 authorizer using IAuthorizerResult with simple responses.
        /// The source generator produces a handler that:
        /// 1. Accepts APIGatewayCustomAuthorizerV2Request
        /// 2. Extracts the Authorization header via [FromHeader]
        /// 3. Calls the user method
        /// 4. Serializes IAuthorizerResult to the simple response format using RouteArn
        /// </summary>
        [LambdaFunction(ResourceName = "SimpleHttpApiAuth", PackageType = LambdaPackageType.Image)]
        [HttpApiAuthorizer(
            EnableSimpleResponses = true,
            AuthorizerPayloadFormatVersion = AuthorizerPayloadFormatVersion.V2)]
        public IAuthorizerResult SimpleHttpApiAuthorizer(
            [FromHeader(Name = "Authorization")] string authorization,
            ILambdaContext context)
        {
            if (string.IsNullOrEmpty(authorization))
                return AuthorizerResults.Deny();

            return AuthorizerResults.Allow()
                .WithContext("userId", "user-123");
        }

        /// <summary>
        /// REST API token authorizer using IAuthorizerResult.
        /// The source generator produces a handler that:
        /// 1. Accepts APIGatewayCustomAuthorizerRequest
        /// 2. Extracts the Authorization header via [FromHeader]
        /// 3. Calls the user method
        /// 4. Serializes IAuthorizerResult to IAM policy format using MethodArn
        /// </summary>
        [LambdaFunction(ResourceName = "SimpleRestApiAuth", PackageType = LambdaPackageType.Image)]
        [RestApiAuthorizer(
            Type = RestApiAuthorizerType.Token,
            IdentityHeader = "Authorization")]
        public IAuthorizerResult SimpleRestApiAuthorizer(
            [FromHeader(Name = "Authorization")] string authorization,
            ILambdaContext context)
        {
            if (string.IsNullOrEmpty(authorization))
                return AuthorizerResults.Deny();

            return AuthorizerResults.Allow()
                .WithPrincipalId("user-123")
                .WithContext("userId", "user-123");
        }
    }
}
