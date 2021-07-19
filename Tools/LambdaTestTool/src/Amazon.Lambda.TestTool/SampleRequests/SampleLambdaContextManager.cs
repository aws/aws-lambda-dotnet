using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Amazon.Lambda.Core;
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
                InvokedFunctionArn = arn
            };

            return defaultContext;
        }
    }
}