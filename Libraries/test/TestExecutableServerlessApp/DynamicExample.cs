using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using System.Threading.Tasks;

namespace TestServerlessApp
{
    public class DynamicExample
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        public dynamic DynamicReturn(string text, ILambdaContext context)
        {
            context.Logger.LogLine(text);
            return text;
        }

        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        public string DynamicInput(dynamic text, ILambdaContext context)
        {
            context.Logger.LogLine(text);
            return text;
        }
    }
}
