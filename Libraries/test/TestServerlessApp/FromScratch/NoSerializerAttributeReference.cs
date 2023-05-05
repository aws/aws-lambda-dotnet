using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp.FromScratch
{
    public class NoSerializerAttributeReference
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        public string ToUpper(string text)
        {
            return text.ToUpper();
        }
    }
}
