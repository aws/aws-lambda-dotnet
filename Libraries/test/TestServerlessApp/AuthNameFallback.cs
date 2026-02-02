using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    /// <summary>
    /// Tests the fallback behavior where [FromCustomAuthorizer] is used without the Name property,
    /// so the parameter name is used as the authorizer context key instead.
    /// </summary>
    public class AuthNameFallback
    {
        /// <summary>
        /// When Name is not specified on [FromCustomAuthorizer], the parameter name 'userId' 
        /// should be used as the key to look up in the authorizer context.
        /// </summary>
        [LambdaFunction(ResourceName = "AuthNameFallbackTest", PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/api/authorizer-fallback")]
        public async Task GetUserId([FromCustomAuthorizer] string userId, ILambdaContext context)
        {
            context.Logger.LogLine($"User ID from authorizer: {userId}");
            await Task.CompletedTask;
        }
    }
}
