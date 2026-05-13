using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class CustomAuthorizerHttpApiExample
    {
        [LambdaFunction(ResourceName = "HttpApiAuthorizerTest", PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/api/authorizer")]
        public async Task HttpApiAuthorizer([FromCustomAuthorizer(Name = "authKey")] string authorizerValue, ILambdaContext context)
        {
            context.Logger.LogLine(authorizerValue);
            await Task.CompletedTask;
        }
    }
}