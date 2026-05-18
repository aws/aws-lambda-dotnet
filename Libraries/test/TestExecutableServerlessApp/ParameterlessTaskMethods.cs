using System.Threading.Tasks;
using Amazon.Lambda.Annotations;

namespace TestServerlessApp
{
    public class ParameterlessTaskMethods
    {
        [LambdaFunction]
        public async Task NoParameterTask()
        {
            await Task.Delay(0);
        }
    }
}
