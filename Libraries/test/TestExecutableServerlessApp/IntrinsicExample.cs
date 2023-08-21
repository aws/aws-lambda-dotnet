using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class IntrinsicExample
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        public void HasIntrinsic(string text, ILambdaContext context)
        {
            context.Logger.LogLine(text);
        }
    }
}
