using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class CustomAuthorizerWithIHttpResultsExample
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/authorizerihttpresults")]
        public IHttpResult AuthorizerWithIHttpResults(
            [FromCustomAuthorizer(Name = "userId")] string userId, 
            ILambdaContext context)
        {
            return HttpResults.Ok($"Hello {userId}");
        }
    }
}
