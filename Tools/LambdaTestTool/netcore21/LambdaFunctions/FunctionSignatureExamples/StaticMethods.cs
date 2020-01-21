using Amazon.Lambda.Core;

namespace FunctionSignatureExamples
{
    public static class StaticMethods
    {

        public static string TheStaticMethod(string input, ILambdaContext context)
        {
            context.Logger.LogLine("Calling TheStaticMethodß");
            return "TheStaticMethod-" + input;            
        }
        
    }
}