using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class CustomAuthorizerRestExample
    {
        [LambdaFunction(ResourceName = "RestAuthorizerTest", PackageType = LambdaPackageType.Image)]
        [RestApi(LambdaHttpMethod.Get, "/rest/authorizer")]
        public async Task RestAuthorizer([FromCustomAuthorizer(Name = "authKey")] string authorizerValue, ILambdaContext context)
        {
            context.Logger.LogLine(authorizerValue);
            await Task.CompletedTask;
        }
    }
}