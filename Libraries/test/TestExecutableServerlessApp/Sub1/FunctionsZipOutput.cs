using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;

namespace TestServerlessApp.Sub1
{
    public class FunctionsZipOutput
    {
        [LambdaFunction(ResourceName = "ToLower")]
        public string ToLower(string text)
        {
            return text.ToUpper();
        }
    }
}
