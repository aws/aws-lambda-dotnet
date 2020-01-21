using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace FunctionSignatureExamples
{
    public class InstanceMethods
    {
        

        public string StringToStringWithContext(string input, ILambdaContext context)
        {
            context.Logger.LogLine("Calling StringToStringWithContext");
            return "StringToStringWithContext-" + input;
        }

        public string NoParameters(ILambdaContext context)
        {
            context.Logger.LogLine("Calling NoParameters");
            return "NoParameters";
        }
        
        
        public void VoidReturn(ILambdaContext context)
        {
            context.Logger.LogLine("Calling VoidReturn");
        }
        
        
        public string NoContextWithParameter(string input)
        {
            return "NoContextWithParameter-" + input;
        }


        public string WithGenericParameter(List<string> values, ILambdaContext context)
        {
            context.Logger.LogLine("Calling WithGenericParameter");
            return string.Join(',', values.ToArray());
        }
    }
}
