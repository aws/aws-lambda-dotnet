using System;
using Amazon.Lambda.TestTool.Runtime.LambdaMocks;

namespace Amazon.Lambda.TestTool.SampleRequests
{
    /// <summary>
    /// This class manages the sample Lambda Context input requests. This includes a default request and saved requests.
    /// </summary>
    public class SampleLambdaContextManager
    {
        public LocalLambdaContext GetSampleContextRequest()
        {
            const string functionName = "testfunction";
            const string functionVersion = "1";
            var arn = $"arn:aws:lambda:us-east-1:accountid:{functionName}:{functionVersion}";
            var defaultContext = new LocalLambdaContext()
            {
                FunctionName = functionName,
                FunctionVersion = functionVersion,
                MemoryLimitInMB = 256,
                AwsRequestId = Guid.NewGuid().ToString().Replace("-", ""),
                InvokedFunctionArn = arn,
                //3 seconds is the default timeout for Lambda functions
                RemainingTime = TimeSpan.FromSeconds(3)
            };

            return defaultContext;
        }
    }
}