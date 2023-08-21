using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using System.Threading.Tasks;

namespace TestServerlessApp
{
    public class TaskExample
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        public async Task TaskReturn(string text, ILambdaContext context)
        {
            context.Logger.LogLine(text);
            await Task.CompletedTask;
        }
    }
}
