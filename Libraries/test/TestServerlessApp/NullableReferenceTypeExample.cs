using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class NullableReferenceTypeExample
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/nullableheaderhttpapi")]
        public void NullableHeaderHttpApi([FromHeader(Name = "MyHeader")] string? text, ILambdaContext context)
        {
            context.Logger.LogLine(text);
        }
    }
}
