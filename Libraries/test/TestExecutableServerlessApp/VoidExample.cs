using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class VoidExample
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        public void VoidReturn(string text, ILambdaContext context)
        {
            context.Logger.LogLine(text);
        }
    }
}
