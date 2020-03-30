using System;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

namespace FunctionSignatureExamples
{
    public class AsyncMethods
    {

        public Task TaskWithNoResult(string input, ILambdaContext context)
        {
            context.Logger.LogLine("Calling TaskWithNoResult");
            return Task.Delay(100);
        }
        
        public async Task<string> TaskWithResult(string input, ILambdaContext context)
        {
            context.Logger.LogLine("Calling TaskWithResult");
            await Task.Delay(100);
            return "TaskWithResult-" + input;
        }
        
    }
}