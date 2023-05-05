using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp.FromScratch
{
    public class NoApiGatewayEventsReference
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/{text}")]
        public string ToUpper(string text)
        {
            return text.ToUpper();
        }
    }
}
