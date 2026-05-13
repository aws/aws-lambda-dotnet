using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class CustomAuthorizerHttpApiV1Example
    {
        [LambdaFunction(ResourceName = "HttpApiV1AuthorizerTest", PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/api/authorizer-v1", Version = HttpApiVersion.V1)]
        public async Task HttpApiV1Authorizer([FromCustomAuthorizer(Name = "authKey")] string authorizerValue, ILambdaContext context)
        {
            context.Logger.LogLine(authorizerValue);
            await Task.CompletedTask;
        }
    }
}
