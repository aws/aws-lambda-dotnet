using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    /// <summary>
    /// Tests [FromCustomAuthorizer] with non-string types (int, bool, double).
    /// The source generator should produce code that uses Convert.ChangeType to handle these.
    /// </summary>
    public class CustomAuthorizerNonStringExample
    {
        /// <summary>
        /// HTTP API v2 endpoint with non-string types from custom authorizer.
        /// </summary>
        [LambdaFunction(ResourceName = "HttpApiNonString")]
        [HttpApi(LambdaHttpMethod.Get, "/api/authorizer-non-string")]
        public async Task HttpApiWithNonString(
            [FromCustomAuthorizer(Name = "userId")] int userId,
            [FromCustomAuthorizer(Name = "isAdmin")] bool isAdmin,
            [FromCustomAuthorizer(Name = "score")] double score,
            ILambdaContext context)
        {
            context.Logger.LogLine($"UserId: {userId}, IsAdmin: {isAdmin}, Score: {score}");
            await Task.CompletedTask;
        }
    }
}
