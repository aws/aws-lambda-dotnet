using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;

namespace TestServerlessApp.Sub1
{
    public class Functions
    {
        [LambdaFunction(Name = "ToUpper")]
        public string ToUpper(string text)
        {
            return text.ToUpper();
        }
    }
}
